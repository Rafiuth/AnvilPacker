using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class RegionEncoderSettings
    {
        public int MetaBrotliQuality = 8;
        public int MetaBrotliWindowSize = 22;

        public RepDataEncMode LightEncMode = RepDataEncMode.Normal;
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
        Strip,  //Don't encode
        Normal, //Encode as is
        Delta   //Encode the differences from the reproduced values
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