#nullable enable

using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AnvilPacker.Data;
using AnvilPacker.Data.Archives;
using AnvilPacker.Encoder;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;

namespace AnvilPacker
{
    public class WorldUnpacker : PackProcessor
    {
        private IArchiveReader _archive;
        private RegionDecoderSettings _decoderSettings;

        public WorldUnpacker(string packPath, string worldPath, RegionDecoderSettings decoderSettings)
        {
            _world = new WorldInfo(worldPath);
            _archive = DataArchive.Open(packPath);
            _decoderSettings = decoderSettings;
        }

        public override async Task Run(int maxThreads)
        {
            ReadMetadata();

            var decodeOpts = new ExecutionDataflowBlockOptions() {
                MaxDegreeOfParallelism = maxThreads,
                EnsureOrdered = false,
                BoundedCapacity = 1024
            };
            var linkOpts = new DataflowLinkOptions() {
                PropagateCompletion = true
            };
            var decodeRegionBlock = new ActionBlock<ArchiveEntry>(DecodeRegion, decodeOpts);
            var decodeOpaqueBlock = new ActionBlock<ArchiveEntry>(DecodeOpaque, decodeOpts);

            foreach (var entry in _archive.ReadEntries()) {
                if (Utils.FileHasExtension(entry.Name, REGION_EXT)) {
                    _regionProgress.AddItem();
                    await decodeRegionBlock.SendAsync(entry);
                } else {
                    _opaqueProgress.AddItem();
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

            var entry = _archive.FindEntry("anvilpacker.json");
            if (entry == null) {
                _logger.Error("Packed world is missing `anvilpacker.json`. Proceeding with default settings...");
                return;
            }
            using var jr = new JsonTextReader(new StreamReader(_archive.OpenEntry(entry), Encoding.UTF8));
            _meta = _metaJsonSerializer.Deserialize<PackMetadata>(jr)!;

            LogStatus($"Pack metadata:");
            LogStatus($"  Encoder version: {_meta.Version}");
            LogStatus($"  Timestamp: {_meta.Timestamp}");

            Ensure.That(_meta.DataVersion <= 1, $"Unsupported metadata version: {_meta.DataVersion}");
        }

        private void DecodeRegion(ArchiveEntry entry)
        {
            LogStatus("Decoding '{0}'...", entry.Name);

            var mem = new MemoryStream();
            using (var es = _archive.OpenEntry(entry)) {
                CopyStream(es, mem, entry.Size);
                mem.Position = 0;
            }
            var region = new RegionBuffer();
            var decoder = new RegionDecoder(region, _decoderSettings);
            decoder.Decode(new DataReader(mem), _regionProgress.CreateProgressListener());

            foreach (var transform in _meta.Transforms) {
                transform.Apply(region);
            }

            var path = Path.ChangeExtension(GetExtractionPath(entry), "mca");
            region.Save(_world, path);
        }

        private void DecodeOpaque(ArchiveEntry entry)
        {
            LogStatus("Extracting '{0}'...", entry.Name);

            string path = GetExtractionPath(entry);
            using var fs = File.Create(path);
            using var es = _archive.OpenEntry(entry);
            CopyStream(es, fs, entry.Size, _opaqueProgress);
        }

        /// <summary> Gets the destination path of the entry. This method also creates directories. </summary>
        private string GetExtractionPath(ArchiveEntry entry)
        {
            string path = Path.Combine(_world.RootPath, entry.Name);
            if (!Utils.IsSubPath(_world.RootPath, path)) {
                var newName = Utils.RemoveInvalidPathChars(entry.Name);
                _logger.Warn($"Malicious entry trying to extract outside world directory: '{entry.Name}' - renaming to '{newName}'.");
                path = Path.Combine(_world.RootPath, newName);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }

        public override void Dispose()
        {
            _archive.Dispose();
        }
    }
}