#nullable enable

using System;
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
        readonly TransformPipe _transforms;
        readonly RegionEncoderSettings _encoderSettings;

        public WorldPacker(string worldPath, string packPath, TransformPipe transforms, RegionEncoderSettings encoderSettings)
            : base(worldPath, packPath)
        {
            _transforms = transforms;
            _encoderSettings = encoderSettings;

            _meta = new() {
                Version = GetInfoVersion(),
                DataVersion = 1,
                Transforms = _transforms.OfType<ReversibleTransform>().Reverse().ToList(),
                Timestamp = DateTimeOffset.Now
            };
        }

        protected override Task Begin()
        {
            WriteMetadata();
            _world = new WorldInfo();
            return Task.CompletedTask;
        }

        protected override FileSink[] CreateSinks()
        {
            return new FileSink[] {
                new RegionEncSink(this, _transforms, _encoderSettings),
                new BlobEncSink(this)
            };
        }

        private void WriteMetadata()
        {
            using var entry = _outArchive.Create(METADATA_PATH);
            using var jw = new JsonTextWriter(new StreamWriter(entry, Encoding.UTF8));
            jw.Formatting = Formatting.Indented;

            _metaJsonSerializer.Serialize(jw, _meta);
        }
    }
}