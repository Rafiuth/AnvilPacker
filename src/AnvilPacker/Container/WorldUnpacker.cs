using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Container.Sinks;
using AnvilPacker.Encoder;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;

namespace AnvilPacker.Container
{
    public class WorldUnpacker : PackProcessor
    {
        readonly RegionDecoderSettings _decoderSettings;

        public WorldUnpacker(string worldPath, string packPath, RegionDecoderSettings decoderSettings)
            : base(worldPath, packPath)
        {
            _decoderSettings = decoderSettings;
        }

        protected override Task Begin()
        {
            ReadMetadata();
            _world = new WorldInfo();
            return Task.CompletedTask;
        }

        protected override void CreateSinks(List<FileSink> sinks)
        {
            sinks.Add(new RegionDecSink(this, _decoderSettings));
            sinks.Add(new BlobDecSink(this));
            sinks.Add(new MetadataDisposerSink(this));
        }

        private void ReadMetadata()
        {
            using var jr = new JsonTextReader(new StreamReader(_inArchive.Open(METADATA_PATH), Encoding.UTF8));
            _meta = _metaJsonSerializer.Deserialize<PackMetadata>(jr)!;

            _logger.Info($"Pack metadata:");
            _logger.Info($"  Encoder version: {_meta.Version}");
            _logger.Info($"  Timestamp: {_meta.Timestamp}");

            Ensure.That(_meta.DataVersion <= 1, $"Unsupported metadata version: {_meta.DataVersion}");
        }
    }
}