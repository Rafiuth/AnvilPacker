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
        public async Task Decode()
        {
            _zip = ZipFile.Open(_zipPath, ZipArchiveMode.Read);
            ReadMetadata();

            var decodeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = _maxThreads,
                EnsureOrdered = false,
                BoundedCapacity = 1024
            };
            var linkOpts = new DataflowLinkOptions() {
                PropagateCompletion = true
            };
            var decodeRegionBlock = new ActionBlock<ZipArchiveEntry>(DecodeRegion, decodeOpts);
            var decodeOpaqueBlock = new ActionBlock<ZipArchiveEntry>(DecodeOpaque, decodeOpts);

            foreach (var entry in _zip.Entries) {
                if (Utils.FileHasExtension(entry.Name, ".apr")) {
                    _regionProgress.AddItem();
                } else {
                    _opaqueProgress.AddItem();
                }
            }

            foreach (var entry in _zip.Entries) {
                if (Utils.FileHasExtension(entry.Name, ".apr")) {
                    await decodeRegionBlock.SendAsync(entry);
                } else {
                    await decodeOpaqueBlock.SendAsync(entry);
                }
            }
            decodeRegionBlock.Complete();
            decodeOpaqueBlock.Complete();
            await decodeRegionBlock.Completion;
            await decodeOpaqueBlock.Completion;
        }

        private void ReadMetadata()
        {
            LogStatus("Reading metadata...");

            var entry = _zip.GetEntry("anvilpacker.json");
            if (entry == null) {
                _logger.Error("Packed world is missing `anvilpacker.json`. Proceeding with default settings...");
                return;
            }
            using var jr = new JsonTextReader(new StreamReader(entry.Open(), Encoding.UTF8));
            _meta = TransformPipe.SettingSerializer.Deserialize<PackMetadata>(jr)!;

            LogStatus($"Pack metadata:");
            LogStatus($"  Encoder version: {_meta.Version}");
            LogStatus($"  Timestamp: {_meta.Timestamp}");

            Ensure.That(_meta.DataVersion <= 1, $"Unsupported metadata version: {_meta.DataVersion}");
        }

        private void DecodeRegion(ZipArchiveEntry entry)
        {
            LogStatus("Decoding '{0}'...", entry.FullName);

            var mem = new MemoryStream();
            using (var es = OpenEntry(entry)) {
                CopyStream(es, mem, entry.Length, readLock: entry.Archive);
                mem.Position = 0;
            }
            var region = new RegionBuffer();
            var decoder = new RegionDecoder(region);
            decoder.Decode(new DataReader(mem), _regionProgress.CreateProgressListener());

            var path = Path.ChangeExtension(GetExtractionPath(entry), "mca");
            region.Save(_world, path);
        }

        private void DecodeOpaque(ZipArchiveEntry entry)
        {
            LogStatus("Extracting '{0}'...", entry.FullName);

            string path = GetExtractionPath(entry);
            using var fs = File.Create(path);
            using var es = OpenEntry(entry);
            CopyStream(es, fs, entry.Length, _opaqueProgress, readLock: entry.Archive);
        }

        private Stream OpenEntry(ZipArchiveEntry entry)
        {
            lock (entry.Archive) {
                return entry.Open();
            }
        }

        /// <summary> Gets the destination path of the entry. This method also creates directories .</summary>
        private string GetExtractionPath(ZipArchiveEntry entry)
        {
            string path = Path.Combine(_world.RootPath, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }
    }
}