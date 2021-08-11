using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class RegionEncoderSettings
    {
        public int MetaBrotliQuality = 8;
        public int MetaBrotliWindowSize = 22;

        public RepDataEncMode LightEncMode = RepDataEncMode.Keep;
        public RepDataEncMode HeightmapEncMode = RepDataEncMode.Strip;

        public BlockCodecSettings BlockCodec = new BlockCodecSettings.AP1();

        public static RegionEncoderSettings Parse(string str)
        {
            var types = new[]{
                ("ap1",     typeof(BlockCodecSettings.AP1)),
                ("brotli",  typeof(BlockCodecSettings.Brotli)),
            };
            var parser = new SettingParser(typeof(RegionEncoderSettings), types);
            return parser.Parse<RegionEncoderSettings>(str);
        }
    }
    /// <summary> Specifies how to encode reproducible data such as lighting and heightmaps. </summary>
    public enum RepDataEncMode
    {
        //Values are encoded, do not change
        Strip = 0, //Don't encode
        Keep  = 1, //Encode as is
        Delta = 2, //Encode the differences from the reproduced values

        Auto = -1, //Strip if no estimation is required (all blocks are known), otherwise, keep.
    }
    public abstract class BlockCodecSettings
    {
        public abstract BlockCodec Create(RegionBuffer region);

        public class AP1 : BlockCodecSettings
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
        public class Brotli : BlockCodecSettings
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
}