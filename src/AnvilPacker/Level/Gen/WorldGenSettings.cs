namespace AnvilPacker.Level.Gen
{
    public class WorldGenSettings
    {
        public long Seed;
        public WorldGenVersion Version  = WorldGenVersion.v1_16_5;

        public int MinY                 = 0;
        public int Height               = 256;

        public int SeaLevel             = 63;

        public double DensityFactor     = 1.0;
        public double DensityOffset     = -0.46875;
        public bool RandomDensityOffset = true;
        public bool IsAmplified         = false;
        public bool UseSimplexSurfaceNoise = true;

        public double NoiseScaleXZ      = 0.9999999814507745;
        public double NoiseScaleY       = 0.9999999814507745;
        public double NoiseFactorXZ     = 80.0;
        public double NoiseFactorY      = 160.0;

        public int TopSlideTarget       = -10;
        public int TopSlideSize         = 3;
        public int TopSlideOffset       = 0;

        public int BottomSlideTarget    = 15;
        public int BottomSlideSize      = 3;
        public int BottomSlideOffset    = 0;


        // <= 1.12.2
        public double LowerLimitScale = 512;
        public double UpperLimitScale = 512;

        public double BaseSize = 8.5;
        public double StretchY = 12.0;
        public double DepthNoiseScaleXZ = 200;
    }
    public enum WorldGenVersion
    {
        //TODO: find min versions
        v1_8,       //1.8-1.12.2
        v1_16_5,
    }
}