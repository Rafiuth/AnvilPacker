using System;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Gen
{
    //Class name mappings:
    //   mojang            ours
    //ImprovedNoise     PerlinNoise
    //PerlinNoise       FractalNoise
    //BlendedNoise      ChunkGenerator.SampleMainNoise()
    public class PerlinNoise
    {
        public readonly double OffsetX, OffsetY, OffsetZ;
        private readonly byte[] _perm;

        public PerlinNoise(WorldGenRandom rng)
        {
            OffsetX = rng.NextDouble() * 256.0;
            OffsetY = rng.NextDouble() * 256.0;
            OffsetZ = rng.NextDouble() * 256.0;
            _perm = new byte[256];
            for (int i = 0; i < 256; i++) {
                _perm[i] = (byte)i;
            }
            for (int i = 0; i < 256; i++) {
                int j = rng.NextInt(256 - i);
                byte temp = _perm[i];
                _perm[i] = _perm[i + j];
                _perm[i + j] = temp;
            }
        }

        public double Sample(double x, double y, double z, double yRoundFactor, double yRoundThreshold)
        {
            x += OffsetX;
            y += OffsetY;
            z += OffsetZ;

            int ix = Floor(x);
            int iy = Floor(y);
            int iz = Floor(z);
            double sx = x - ix;
            double sy = y - iy;
            double sz = z - iz;

            double actualSy = sy;

            if (yRoundFactor != 0.0) {
                double threshold = yRoundThreshold >= 0.0 && yRoundThreshold < sy ? yRoundThreshold : sy;
                double offset = Floor(threshold / yRoundFactor + 1.0E-7) * yRoundFactor;
                sy -= offset;
            }
            int A  = Hash(ix);
            int B  = Hash(ix + 1);
            int AA = Hash(A + iy);
            int AB = Hash(A + iy + 1);
            int BA = Hash(B + iy);
            int BB = Hash(B + iy + 1);
            double v000 = Grad(Hash(AA + iz    ), sx,     sy,     sz    );
            double v100 = Grad(Hash(BA + iz    ), sx - 1, sy,     sz    );
            double v010 = Grad(Hash(AB + iz    ), sx,     sy - 1, sz    );
            double v110 = Grad(Hash(BB + iz    ), sx - 1, sy - 1, sz    );
            double v001 = Grad(Hash(AA + iz + 1), sx,     sy,     sz - 1);
            double v101 = Grad(Hash(BA + iz + 1), sx - 1, sy,     sz - 1);
            double v011 = Grad(Hash(AB + iz + 1), sx,     sy - 1, sz - 1);
            double v111 = Grad(Hash(BB + iz + 1), sx - 1, sy - 1, sz - 1);

            double tx = Fade(sx);
            double ty = Fade(actualSy);
            double tz = Fade(sz);

            return Lerp(
                Lerp(
                    Lerp(v000, v100, tx),
                    Lerp(v010, v110, tx),
                    ty
                ),
                Lerp(
                    Lerp(v001, v101, tx),
                    Lerp(v011, v111, tx),
                    ty
                ),
                tz
            );
        }

        private static double Grad(int hash, double x, double y, double z)
        {
            ref var grad = ref GRADIENTS[hash & 15];
            return grad.X * x + grad.Y * y + grad.Z * z;
        }
        private int Hash(int x)
        {
            return _perm[x & 255];
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
        private static double Fade(double x)
        {
            return x * x * x * (x * (x * 6 - 15) + 10);
        }
        private static int Floor(double x)
        {
            return (int)Math.Floor(x);
        }

        private static readonly (double X, double Y, double Z)[] GRADIENTS = new (double X, double Y, double Z)[] {
            ( 1,  1,  0), (-1,  1,  0), ( 1, -1,  0), (-1, -1,  0),
            ( 1,  0,  1), (-1,  0,  1), ( 1,  0, -1), (-1,  0, -1),
            ( 0,  1,  1), ( 0, -1,  1), ( 0,  1, -1), ( 0, -1, -1),
            ( 1,  1,  0), ( 0, -1,  1), (-1,  1,  0), ( 0, -1, -1)
        };
    }

    public class FractalNoise
    {
        public readonly PerlinNoise[] Octaves;

        public FractalNoise(WorldGenRandom rng, int numOctaves)
        {
            Octaves = new PerlinNoise[numOctaves];

            for (int i = 0; i < numOctaves; i++) {
                Octaves[i] = new PerlinNoise(rng);
            }
        }

        /// <summary>
        /// Calculates the fractal sample at the specified position, with
        /// frequency decreasing and amplitude increasing for each octave.
        /// `Freq[i] = 2^-i, Amplitude[i] = 2^i`
        /// </summary>
        public double SampleExp(double x, double y, double z, double xzScale, double yScale, bool wrap = true, bool is2D = false)
        {
            double freq = 1.0;
            double result = 0.0;

            foreach (var octave in Octaves) {
                double nx = x * xzScale * freq;
                double ny = y * yScale  * freq;
                double nz = z * xzScale * freq;
                if (wrap) {
                    nx = Wrap(nx);
                    ny = Wrap(ny);
                    nz = Wrap(nz);
                }
                if (is2D) {
                    ny = -octave.OffsetY;
                }
                result += octave.Sample(nx, ny, nz, yScale * freq, y * yScale * freq) / freq;
                freq /= 2.0;
            }
            return result;
        }

        /// <summary>
        /// Calculates the fractal sample at the specified position, with
        /// frequency increasing and amplitude decreasing for each octave.
        /// `Freq[i] = 0.5/2^i, Amplitude[i] ~ 1/2^(n-i)`
        /// </summary>
        public double SampleRev(double x, double y, double z, double yRoundFactor, double yRoundThreshold, bool is2D = false)
        {
            //double freq = Math.Pow(2.0, -(_octaves.Length - 1));
            //double ampl = Math.Pow(2.0, _octaves.Length - 1) / (Math.Pow(2.0, _octaves.Length) - 1.0); //why tf

            double expOctaves = 1L << Octaves.Length;
            double freq = 2.0 / expOctaves;
            double amplitude = expOctaves / (expOctaves - 1) * 0.5;

            double result = 0.0;

            for (int i = Octaves.Length - 1; i >= 0; i--) {
                var octave = Octaves[i];
                double noise = octave.Sample(
                    Wrap(x * freq),
                    is2D ? -octave.OffsetY : Wrap(y * freq),
                    Wrap(z * freq),
                    yRoundFactor * freq, 
                    yRoundThreshold * freq
                );
                result += noise * amplitude;

                freq *= 2.0;
                amplitude /= 2.0;
            }
            return result;
        }

        private static double Wrap(double x)
        {
            //guess: `x mod L`
            const double L = 33554432; //2^25
            return x - Math.Floor(x / L + 0.5) * L;
        }
    }
}