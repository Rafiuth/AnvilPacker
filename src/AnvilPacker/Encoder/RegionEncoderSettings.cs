using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class RegionEncoderSettings
    {
        public int MetaBrotliQuality = 8;
        public int MetaBrotliWindowSize = 22;

        public BlockCodecSettings BlockCodec = new BlockCodecSettings_AP1();

        public static RegionEncoderSettings Parse(string str)
        {
            var types = new[]{
                ("ap1",     typeof(BlockCodecSettings_AP1)),
                ("brotli",  typeof(BlockCodecSettings_Brotli)),
            };
            var parser = new SettingParser(typeof(RegionEncoderSettings), types);
            return parser.Parse<RegionEncoderSettings>(str);
        }
    }
    public abstract class BlockCodecSettings
    {
        public abstract BlockCodec Create(RegionBuffer region);
    }

    public class BlockCodecSettings_AP1 : BlockCodecSettings
    {
        public int ContextBits = 13;
        public Vec3i[] Neighbors = v1.BlockCodecV1.DefaultNeighbors;

        public override BlockCodec Create(RegionBuffer region)
        {
            return new v1.BlockCodecV1(region) {
                ContextBits = ContextBits,
                Neighbors = Neighbors
            };
        }
    }
    public class BlockCodecSettings_Brotli : BlockCodecSettings
    {
        public int Quality = 6;
        public int WindowSize = 22;

        public override BlockCodec Create(RegionBuffer region)
        {
            return new v2.BlockCodecBrotli(region) {
                Quality = Quality,
                WindowSize = WindowSize
            };
        }
    }
}