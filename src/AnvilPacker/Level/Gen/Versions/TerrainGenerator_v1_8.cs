using System;
using System.IO;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Gen
{
    public class TerrainGenerator_v1_8 : TerrainGenerator
    {
        public TerrainGenerator_v1_8(WorldGenSettings settings)
            : base(settings)
        {
        }

        protected override void GenerateNoiseColumn(int x, int z)
        {
            int wx = _chunk.X * NoiseW + x;
            int wz = _chunk.Z * NoiseD + z;

            var (depth, scale) = GetBiomeInfluence(x, z);
            depth = ((float)depth * 4.0f - 1.0f) / 8.0f;
            scale = (float)scale * 0.9f + 0.1f;

            double h = _depthNoise.SampleExp(wx, 0, wz, Settings.DepthNoiseScaleXZ, 0, true, true) / 8000.0;

            if (h < 0.0) {
                h = -h * 0.3;
            }

            h = h * 3.0 - 2.0;

            if (h < 0.0) {
                h = h / 2.0;

                if (h < -1.0) {
                    h = -1.0;
                }
                h = h / 1.4;
                h = h / 2.0;
            } else {
                if (h > 1.0) {
                    h = 1.0;
                }
                h = h / 8.0;
            }
            h = depth + h * 0.2;
            h = h * Settings.BaseSize / 8.0;
            h = Settings.BaseSize + h * 4.0;

            double limitScaleXZ = 684.412 * Settings.NoiseScaleXZ;
            double limitScaleY = 684.412 * Settings.NoiseScaleY;
            double mainScaleXZ = limitScaleXZ / Settings.NoiseFactorXZ;
            double mainScaleY = limitScaleY / Settings.NoiseFactorY;

            for (int y = 0; y < 33; y++) {
                double vertDensity = (y - h) * Settings.StretchY * 128.0 / 256.0 / scale;

                if (vertDensity < 0.0) {
                    vertDensity *= 4.0;
                }

                double minLimit = _minLimitNoise.SampleExp(wx, y, wz, limitScaleXZ, limitScaleY) / Settings.LowerLimitScale;
                double maxLimit = _maxLimitNoise.SampleExp(wx, y, wz, limitScaleXZ, limitScaleY) / Settings.UpperLimitScale;
                double main = _mainNoise.SampleExp(wx, y, wz, mainScaleXZ, mainScaleY);
                double density = Maths.ClampedLerp(minLimit, maxLimit, (main / 10.0 + 1.0) / 2.0) - vertDensity;

                if (y > 29) { //top slide
                    double t = (y - 29) / 3.0f;
                    density = density * (1.0 - t) + -10.0 * t;
                }
                Noise(x, y, z) = density;
            }
        }
    }
}