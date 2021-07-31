#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Container.Sinks;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;

namespace AnvilPacker.Container
{
    public partial class WorldPacker : PackProcessor
    {
        readonly WorldPackerSettings _settings;

        public WorldPacker(string worldPath, string packPath, WorldPackerSettings settings)
            : base(worldPath, packPath)
        {
            _settings = settings;
        }

        protected override Task Begin()
        {
            _meta = new() {
                Version = GetInfoVersion(),
                DataVersion = 1,
                Transforms = _settings.Transforms.OfType<ReversibleTransform>().Reverse().ToList(),
                Timestamp = DateTimeOffset.Now
            };
            _world = new WorldInfo();
            WriteMetadata();
            return Task.CompletedTask;
        }

        protected override void CreateSinks(List<FileSink> sinks)
        {
            sinks.Add(new RegionEncSink(this, _settings.Transforms, _settings.EncoderSettings));
            if (_settings.DisableBlobs) {
                sinks.Add(new BlobEncSink(this));
            }
        }

        private void WriteMetadata()
        {
            using var entry = _outArchive.Create(METADATA_PATH);
            using var jw = new JsonTextWriter(new StreamWriter(entry, Encoding.UTF8));
            jw.Formatting = Formatting.Indented;

            _metaJsonSerializer.Serialize(jw, _meta);
        }
    }

    public class WorldPackerSettings
    {
        public TransformPipe Transforms { get; init; } = null!;
        public RegionEncoderSettings EncoderSettings { get; init; } = null!;
        public bool DisableBlobs { get; init; }
    }
}