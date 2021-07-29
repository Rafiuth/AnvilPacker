#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Container.Sinks
{
    public abstract class RegionSink : FileSink
    {
        protected readonly RegionBuffer _region = new();

        protected RegionSink(PackProcessor packer) : base(packer)
        {
        }
    }
    public class RegionEncSink : RegionSink
    {
        private readonly TransformPipe _transforms;
        private readonly RegionEncoderSettings _encoderSettings;

        public RegionEncSink(PackProcessor packer, TransformPipe transforms, RegionEncoderSettings encoderSettings)
            : base(packer)
        {
            _transforms = transforms;
            _encoderSettings = encoderSettings;
        }

        public override bool Accepts(string filename, long length)
            => filename.EndsWithIgnoreCase(".mca");

        public override Task Process(string filename, IProgress<double> progress)
        {
            int numChunks = 0;

            using (var reader = new RegionReader(OpenFaucet(filename), filename)) {
                numChunks = _region.Load(_packer._world, reader, filename);
            }

            if (numChunks > 0) {
                _transforms.Apply(_region);
                numChunks = _region.ExistingChunks.Count();
            }
            if (numChunks == 0) {
                _logger.Info("Discarding empty region '{0}'", filename);
                return Task.CompletedTask;
            }
            var outPath = Path.ChangeExtension(filename, PackProcessor.ENC_REGION_EXT);
            using (var outs = new DataWriter(OpenDrain(outPath, CompressionLevel.NoCompression))) {
                var encoder = new RegionEncoder(_region, _encoderSettings);
                encoder.Encode(outs, progress);
            }
            return Task.CompletedTask;
        }
    }
    public class RegionDecSink : RegionSink
    {
        private readonly RegionDecoderSettings _decoderSettings;

        public RegionDecSink(PackProcessor packer, RegionDecoderSettings decoderSettings)
            : base(packer)
        {
            _decoderSettings = decoderSettings;
        }

        public override bool Accepts(string filename, long length)
            => filename.EndsWithIgnoreCase(PackProcessor.ENC_REGION_EXT);

        public override Task Process(string filename, IProgress<double> progress)
        {
            using (var stream = new DataReader(OpenFaucet(filename))) {
                var decoder = new RegionDecoder(_region, _decoderSettings);
                decoder.Decode(stream, progress);
            }

            foreach (var transform in _packer._meta.Transforms) {
                transform.Apply(_region);
            }

            var outPath = Path.ChangeExtension(filename, ".mca");
            using (var writer = new RegionWriter(outPath)) {
                _region.Save(_packer._world, writer);
            }
            return Task.CompletedTask;
        }
    }
}