using System;
using System.Diagnostics;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Gen
{
    /// <summary>
    /// Generates vanilla compatible terrain.
    /// It is not 100% identical to vanilla terrain (missing features), 
    /// but close enough to use for delta coding.
    /// </summary>
    public class VanillaChunkGenerator
    {
        public WorldGenSettings Settings { get; }

        private TerrainGenerator _terrainGen;
        private WorldGenBlocks _blocks = new();

        public VanillaChunkGenerator(WorldGenSettings settings)
        {
            Settings = settings;
            _terrainGen = TerrainGenerator.Create(settings);
        }

        public void Generate(Chunk chunk, BiomeProvider biomeProvider = null)
        {
            _blocks.Update(chunk, Settings);

            _terrainGen.Generate(chunk, biomeProvider, _blocks);

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    int y = Settings.Height - 1;
                    for (; y >= Settings.SeaLevel; y--) {
                        if (chunk.GetBlock(x, y, z).Material.IsSolid) break;
                    }
                    if (y < Settings.SeaLevel - 1) continue;

                    var biome = biomeProvider.Get(chunk.X * 16 + x, chunk.Z * 16 + z);
                    int startY = y;
                    int endY = y - 5;
                    for (; y >= endY; y--) {
                        if (!chunk.GetBlock(x, y, z).Material.IsSolid || y < endY) break;

                        chunk.SetBlockId(x, y, z, biome == Biome.Desert ? _blocks.Sand : (y == startY ? _blocks.Grass : _blocks.Dirt));
                    }
                }
            }
        }
    }
}