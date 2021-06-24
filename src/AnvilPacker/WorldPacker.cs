#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AnvilPacker.Data;
using AnvilPacker.Data.Archives;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;
using NLog;

namespace AnvilPacker
{
    //https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library
    public class WorldPacker : WorldPackProcessor
    {
        private IArchiveWriter _archive;
        private TransformPipe _transforms;

        public WorldPacker(string worldPath, string packPath)
        {
            _world = new WorldInfo(worldPath);
            _archive = DataArchive.Create(packPath);

            _transforms = TransformPipe.Empty;

            _meta = new() {
                Version = GetVersion(),
                DataVersion = 1,
                Transforms = _transforms.OfType<ReversibleTransform>().Reverse().ToList(),
                Timestamp = DateTime.UtcNow
            };
        }

        public override async Task Run(int maxThreads)
        {
            var encodeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = maxThreads,
                EnsureOrdered = false,
                BoundedCapacity = 65536
            };
            var writeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = _archive.SupportsSimultaneousWrites ? maxThreads : 1,
                EnsureOrdered = false,
                BoundedCapacity = maxThreads
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
            using var entry = _archive.CreateEntry("anvilpacker.json");
            using var jw = new JsonTextWriter(new StreamWriter(entry, Encoding.UTF8));
            jw.Formatting = Formatting.Indented;
            TransformPipe.SettingSerializer.Serialize(jw, _meta);
        }

        private WriteMessage EncodeRegion(string path)
        {
            string relPath = Path.GetRelativePath(_world.RootPath, path);
            LogStatus("Encoding '{0}'...", relPath);

            //TODO: LoadRegion in a separate data flow block could improve perf with slow IO devices
            var region = new RegionBuffer();
            int numChunks = 0;

            try {
                numChunks = region.Load(_world, path);
            } catch (Exception ex) {
                _logger.Error(ex, "Failed to load region '{0}'. Copying to the output as is.", relPath);
                return new WriteMessage() {
                    Name = relPath,
                    InputFileName = path,
                    Progress = _regionProgress,
                    Compress = true
                };
            }
            if (numChunks > 0) {
                _transforms.Apply(region);
                numChunks = region.Chunks.Count(c => c != null);
            }

            if (numChunks == 0) {
                LogStatus("Discarding empty region '{0}'", path);
                _regionProgress.Inc(1.0);
                return default;
            }
            var encoder = new RegionEncoder(region);
            var mem = new MemoryDataWriter(1024 * 1024 * 4);
            encoder.Encode(mem, _regionProgress.CreateProgressListener());

            return new WriteMessage() {
                Name = Path.ChangeExtension(relPath, REGION_EXT),
                Data = mem.BufferMem,
                Compress = false
            };
        }

        private static readonly string[] UNCOMPRESSABLE_FILE_EXTS = {
            ".dat", ".nbt", ".schematic",   //gzipped
            ".png", ".jpg",
            ".zip", ".rar", ".7z"
        };
        private WriteMessage EncodeOpaque(string path)
        {
            return new WriteMessage() {
                Name = Path.GetRelativePath(_world.RootPath, path),
                InputFileName = path,
                Compress = !Utils.FileHasExtension(path, UNCOMPRESSABLE_FILE_EXTS),
                Progress = _opaqueProgress
            };
        }

        private void WriteEntry(WriteMessage msg)
        {
            if (msg.Name == null) {
                return;
            }
            LogStatus("Writing '{0}'...", msg.Name);

            var compLevel = msg.Compress ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
            using var es = _archive.CreateEntry(msg.Name, compLevel);

            if (msg.InputFileName != null) {
                using var fs = File.OpenRead(msg.InputFileName);
                CopyStream(fs, es, fs.Length, msg.Progress);
            } else {
                es.Write(msg.Data.Span);
            }
        }

        private string GetVersion()
        {
            var asm = typeof(WorldPacker).Assembly;
            var attrib = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attrib?.InformationalVersion ?? asm.GetName().Version!.ToString();
        }

        //Holds info on a pending entry to be written into the output archive
        struct WriteMessage
        {
            public string Name;
            public string? InputFileName;   //If null, Data will be written instead
            public Memory<byte> Data;
            public bool Compress;
            public PackerTaskProgress? Progress;
        }

        public override void Dispose()
        {
            _archive?.Dispose();
        }
    }
}