using System;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Gen
{
    public class TerrainGenerator_v1_16_5 : TerrainGenerator
    {
        public TerrainGenerator_v1_16_5(WorldGenSettings settings)
            : base(settings)
        {
        }

        protected override void GenerateNoiseColumn(int x, int z)
        {
            int wx = _chunk.X * NoiseW + x;
            int wz = _chunk.Z * NoiseD + z;

            var (depth, scale) = GetBiomeInfluence(x, z);
            depth = (float)depth * 0.5f - 0.125f;
            scale = (float)scale * 0.9f + 0.1f;
            depth = depth * 0.265625;
            scale = 96.0 / scale;

            double scaleXZ = 684.412 * Settings.NoiseScaleXZ;
            double scaleY = 684.412 * Settings.NoiseScaleY;
            double mainScaleXZ = scaleXZ / Settings.NoiseFactorXZ;
            double mainScaleY = scaleY / Settings.NoiseFactorY;

            double baseDensity = Settings.RandomDensityOffset ? GetRandomDensity(wx, wz) : 0.0;

            for (int y = 0; y <= NoiseH; y++) {
                double vertDensity = 1.0 - y * 2.0 / NoiseH + baseDensity;
                vertDensity = vertDensity * Settings.DensityFactor + Settings.DensityOffset;
                vertDensity = (vertDensity + depth) * scale;
                if (vertDensity > 0.0) {
                    vertDensity *= 4.0;
                }

                double density = SampleAndClampNoise(wx, y, wz, scaleXZ, scaleY, mainScaleXZ, mainScaleY);
                density += vertDensity;

                if (Settings.TopSlideSize > 0.0) {
                    double t = ((NoiseH - y) - Settings.TopSlideOffset) / (double)Settings.TopSlideSize;
                    density = Maths.ClampedLerp(Settings.TopSlideTarget, density, t);
                }
                if (Settings.BottomSlideSize > 0.0) {
                    double t = (y - Settings.BottomSlideOffset) / (double)Settings.BottomSlideSize;
                    density = Maths.ClampedLerp(Settings.BottomSlideTarget, density, t);
                }
                Noise(x, y, z) = density;
            }
        }

        private double SampleAndClampNoise(int x, int y, int z, double limitScaleXZ, double limitScaleY, double mainScaleXZ, double mainScaleY)
        {
            double minLimit = _minLimitNoise.SampleExp(x, y, z, limitScaleXZ, limitScaleY);
            double maxLimit = _maxLimitNoise.SampleExp(x, y, z, limitScaleXZ, limitScaleY);
            double main = _mainNoise.SampleExp(x, y, z, mainScaleXZ, mainScaleY);

            return Maths.ClampedLerp(minLimit / 512.0, maxLimit / 512.0, (main / 10.0 + 1.0) / 2.0);
        }
        private double GetRandomDensity(int x, int z)
        {
            double d = _depthNoise.SampleRev(x * 200, 10.0, z * 200, 1.0, 0.0, true);
            if (d < 0.0) {
                d = -d * 0.3;
            }
            d = d * 24.575625 - 2.0;
            if (d < 0.0) {
                return d * 0.009486607142857142;
            }
            return Math.Min(d, 1.0) * 0.006640625;
        }
    }
}