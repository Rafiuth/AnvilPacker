#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AnvilPacker.Data;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace AnvilPacker
{
    public partial class WorldPacker : IDisposable
    {
        public async Task Encode()
        {
            _zip = ZipFile.Open(_zipPath, ZipArchiveMode.Create);

            var encodeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = _maxThreads,
                EnsureOrdered = false,
                BoundedCapacity = 65536
            };
            var writeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = false,
                BoundedCapacity = _maxThreads
            };
            var linkOpts = new DataflowLinkOptions() {
                PropagateCompletion = true
            };
            var encodeRegionBlock = new TransformBlock<string, WriteMessage>(EncodeRegion, encodeOpts);
            var encodeOpaqueBlock = new TransformBlock<string, WriteMessage>(EncodeOpaque, encodeOpts);
            var writeBlock = new ActionBlock<WriteMessage>(WriteEntry, writeOpts);

            encodeRegionBlock.LinkTo(writeBlock, linkOpts);
            encodeOpaqueBlock.LinkTo(writeBlock, linkOpts);

            LogStatus("Discovering and encoding world files...");

            await QueueFiles(_world.RootPath);
            encodeRegionBlock.Complete();
            //readOpaqueBlock.Complete();

            LogStatus("File discovery done, waiting for encoder to finish...");

            await writeBlock.Completion;

            WriteMetadata();

            async Task QueueFiles(string path)
            {
                foreach (var file in Directory.EnumerateFiles(path)) {
                    if (Utils.FileHasExtension(file, ".mca")) {
                        _regionProgress.AddItem();
                        await encodeRegionBlock.SendAsync(file);
                    } else {
                        _opaqueProgress.AddItem();
                        await encodeOpaqueBlock.SendAsync(file);
                    }
                }
                foreach (var dir in Directory.EnumerateDirectories(path)) {
                    await QueueFiles(dir);
                }
            }
        }

        private void WriteMetadata()
        {
            var entry = _zip.CreateEntry("anvilpacker.json");
            using var jw = new JsonTextWriter(new StreamWriter(entry.Open(), Encoding.UTF8));
            jw.Formatting = Formatting.Indented;
            TransformPipe.SettingSerializer.Serialize(jw, _meta);
        }

        private WriteMessage EncodeRegion(string filename)
        {
            LogStatus("Encoding '{0}'...", filename);
            //TODO: LoadRegion in a separate data flow block could improve perf with slow IO devices
            var region = new RegionBuffer();
            if (region.Load(_world, filename) == 0) {
                LogStatus("Discarding empty region '{0}'", filename);
                _regionProgress.Inc(1.0);
                return default;
            }
            var encoder = new RegionEncoder(region);
            var mem = new MemoryDataWriter(1024 * 1024 * 4);
            encoder.Encode(mem, _regionProgress.CreateProgressListener());

            var entryName = Path.GetRelativePath(_world.RootPath, filename);
            entryName = Path.ChangeExtension(entryName, "apr");

            return new WriteMessage() {
                Name = entryName,
                Data = mem.BufferMem,
                Compress = false
            };
        }

        private static readonly string[] UNCOMPRESSABLE_FILE_EXTS = {
            ".dat", ".nbt", ".schematic",   //gzipped
            ".png", ".jpg",
            ".zip", ".rar", ".7z"
        };
        private WriteMessage EncodeOpaque(string filename)
        {
            var msg = new WriteMessage() {
                Name = Path.GetRelativePath(_world.RootPath, filename),
                InputFileName = filename,
                Compress = !Utils.FileHasExtension(filename, UNCOMPRESSABLE_FILE_EXTS)
            };
            return msg;
        }

        private void WriteEntry(WriteMessage msg)
        {
            if (msg.Name == null) {
                return;
            }
            LogStatus("Writing '{0}'...", msg.Name);

            var compLevel = msg.Compress ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
            var entry = _zip.CreateEntry(msg.Name, compLevel);

            using (var es = entry.Open()) {
                if (msg.InputFileName != null) {
                    using var fs = File.OpenRead(msg.InputFileName);
                    CopyStream(fs, es, fs.Length, _opaqueProgress);
                } else {
                    es.Write(msg.Data.Span);
                }
            }
        }

        //Holds info on a pending entry to be written into the output zip
        struct WriteMessage
        {
            public string Name;
            public string? InputFileName;   //If null, Data will be written instead
            public Memory<byte> Data;
            public bool Compress;
        }
    }
}