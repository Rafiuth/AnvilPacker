using System;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Gen
{
    public abstract class TerrainGenerator
    {
        public readonly WorldGenSettings Settings;

        private readonly double[] _noise;
        public readonly int NoiseW, NoiseH, NoiseD;
        public readonly int NoiseCellW, NoiseCellH;

        protected FractalNoise _minLimitNoise, _maxLimitNoise, _mainNoise;
        protected FractalNoise _depthNoise;

        protected Chunk _chunk; //current chunk being generated
        protected BiomeInfluence[] _biomes; //chunk biomes (depth, scale) for the noise map, +5x5 for the edge smoothing filter

        public static TerrainGenerator Create(WorldGenSettings settings)
        {
            return settings.Version switch {
                >= WorldGenVersion.v1_16_5   => new TerrainGenerator_v1_16_5(settings),
                >= WorldGenVersion.v1_8      => new TerrainGenerator_v1_8(settings),
                _ => throw new NotSupportedException()
            };
        }

        public TerrainGenerator(WorldGenSettings settings)
        {
            Settings = settings;

            NoiseCellW = 4;
            NoiseCellH = 8;

            NoiseW = 16 / NoiseCellW;
            NoiseD = 16 / NoiseCellW;
            NoiseH = settings.Height / NoiseCellH;
            _noise = new double[(NoiseW + 1) * (NoiseH + 1) * (NoiseD + 1)];
            _biomes = new BiomeInfluence[(NoiseW + 1 + 5) * (NoiseD + 1 + 5)];

            var rng = new WorldGenRandom(settings.Seed);

            _minLimitNoise = new FractalNoise(rng, 16);
            _maxLimitNoise = new FractalNoise(rng, 16);
            _mainNoise = new FractalNoise(rng, 8);

            rng.Skip(262 * 4); //surfaceNoise
            rng.Skip(262 * 10); //scaleNoise

            _depthNoise = new FractalNoise(rng, 16);
        }

        public void Generate(Chunk chunk, BiomeProvider biomeProvider, WorldGenBlocks blocks)
        {
            _chunk = chunk;

            UpdateBiomes(biomeProvider);

            GenerateNoise();

            VisitNoise((x, y, z, density) => {
                if (density > 0.0) {
                    _chunk.SetBlockId(x, y, z, blocks.Stone);
                } else if (y < Settings.SeaLevel) {
                    _chunk.SetBlockId(x, y, z, blocks.Water);
                }
            });
        }
        private void GenerateNoise()
        {
            for (int x = 0; x <= NoiseW; x++) {
                for (int z = 0; z <= NoiseD; z++) {
                    GenerateNoiseColumn(x, z);
                }
            }
        }
        protected abstract void GenerateNoiseColumn(int x, int z);

        private void UpdateBiomes(BiomeProvider provider)
        {
            if (provider == null) {
                Array.Fill(_biomes, new BiomeInfluence(Level.Biome.Plains));
                return;
            }
            int cx = _chunk.X << 4;
            int cz = _chunk.Z << 4;
            int sx = 16 / NoiseW;
            int sz = 16 / NoiseD;

            for (int z = -2; z <= NoiseD + 3; z++) {
                for (int x = -2; x <= NoiseW + 3; x++) {
                    var biome = provider.Get(cx + x * sx, cz + z * sz);
                    Biome(x, z) = new BiomeInfluence(biome);
                }
            }
        }
        protected ref double Noise(int x, int y, int z)
        {
            return ref _noise[(x * (NoiseW + 1) + z) * (NoiseH + 1) + y];
        }
        protected ref BiomeInfluence Biome(int x, int z)
        {
            return ref _biomes[(x + 2) + (z + 2) * (NoiseW + 1 + 5)];
        }

        private void VisitNoise(Action<int, int, int, double> visitSample)
        {
            for (int cx = 0; cx < NoiseW; cx++) {
                for (int cz = 0; cz < NoiseD; cz++) {
                    for (int cy = 0; cy < NoiseH; cy++) {
                        VisitNoiseCell(cx, cz, cy, visitSample);
                    }
                }
            }
        }
        private void VisitNoiseCell(int cx, int cz, int cy, Action<int, int, int, double> visitSample)
        {
            double deltaXZ = 1.0 / NoiseCellW;
            double deltaY = 1.0 / NoiseCellH;

            double v000 = Noise(cx + 0, cy + 0, cz + 0);
            double v100 = Noise(cx + 1, cy + 0, cz + 0);
            double v001 = Noise(cx + 0, cy + 0, cz + 1);
            double v101 = Noise(cx + 1, cy + 0, cz + 1);
            double v010 = Noise(cx + 0, cy + 1, cz + 0);
            double v110 = Noise(cx + 1, cy + 1, cz + 0);
            double v011 = Noise(cx + 0, cy + 1, cz + 1);
            double v111 = Noise(cx + 1, cy + 1, cz + 1);

            for (int sy = 0; sy < NoiseCellH; sy++) {
                for (int sz = 0; sz < NoiseCellW; sz++) {
                    for (int sx = 0; sx < NoiseCellW; sx++) {
                        double v = Maths.Lerp3(
                            v000, v100, v010, v110,
                            v001, v101, v011, v111,
                            sx * deltaXZ, sy * deltaY, sz * deltaXZ
                        );
                        visitSample(
                            cx * NoiseCellW + sx,
                            cy * NoiseCellH + sy,
                            cz * NoiseCellW + sz,
                            v
                        );
                    }
                }
            }
        }

        protected (double Depth, double Scale) GetBiomeInfluence(int x, int z)
        {
            //core algorithm unchanged since (prior?) 1.8-1.17
            float depthSum = 0.0f;
            float scaleSum = 0.0f;
            float weightSum = 0.0f;

            var baseBiome = Biome(x, z);

            for (int zo = -2; zo <= 2; zo++) {
                for (int xo = -2; xo <= 2; xo++) {
                    var biome = Biome(x + xo, z + zo);
                    float depth = biome.Depth;
                    float scale = biome.Scale;

                    if (Settings.IsAmplified && depth > 0.0f) {
                        depth = 1.0f + depth * 2.0f;
                        scale = 1.0f + scale * 4.0f;
                    }
                    float weight = Weight(xo, zo) / (depth + 2.0f);
                    if (biome.Depth > baseBiome.Depth) {
                        weight *= 0.5f;
                    }
                    depthSum += depth * weight;
                    scaleSum += scale * weight;
                    weightSum += weight;
                }
            }
            return (depthSum / weightSum, scaleSum / weightSum);

            static float Weight(int x, int z)
            {
                return 10.0f / MathF.Sqrt((x * x + z * z) + 0.2f);
            }
        }

        protected struct BiomeInfluence
        {
            public float Depth, Scale;

            public BiomeInfluence(Biome biome)
            {
                Depth = biome.Depth;
                Scale = biome.Scale;
            }
            public BiomeInfluence(float depth, float scale)
            {
                Depth = depth;
                Scale = scale;
            }
        }
    }
}