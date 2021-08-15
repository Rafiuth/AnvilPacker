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
        private readonly MemoryDataWriter _mem = new MemoryDataWriter(1024 * 1024 * 4);

        public RegionEncSink(PackProcessor packer, TransformPipe transforms, RegionEncoderSettings encoderSettings)
            : base(packer)
        {
            _transforms = transforms;
            _encoderSettings = encoderSettings;
        }

        public override bool Accepts(string filename, long length)
            => filename.EndsWithIgnoreCase(".mca");

        public override async Task Process(string filename, IProgress<double> progress)
        {
            int numChunks = 0;

            using (var reader = new RegionReader(await OpenFaucet(filename), filename)) {
                numChunks = _region.Load(_packer._world, reader, filename);
            }

            if (numChunks > 0) {
                _transforms.Apply(_region);
                numChunks = _region.ExistingChunks.Count();
            }
            if (numChunks == 0) {
                _logger.Info("Discarding empty region '{0}'", filename);
                return;
            }
            _mem.Clear();

            var encoder = new RegionEncoder(_region, _encoderSettings);
            encoder.Encode(_mem, progress);

            var outPath = Path.ChangeExtension(filename, PackProcessor.ENC_REGION_EXT);
            using (var outStream = await OpenDrain(outPath, CompressionLevel.NoCompression)) {
                await outStream.WriteAsync(_mem.BufferMem);
            }
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

        public override async Task Process(string filename, IProgress<double> progress)
        {
            using (var stream = new DataReader(await OpenFaucet(filename))) {
                var decoder = new RegionDecoder(_region, _decoderSettings);
                decoder.Decode(stream, progress);
            }

            foreach (var transform in _packer._meta.Transforms) {
                transform.Apply(_region);
            }

            var outPath = Path.ChangeExtension(filename, ".mca");
            using (var writer = new RegionWriter(await OpenDrain(outPath))) {
                _region.Save(_packer._world, writer);
            }
        }
    }
}