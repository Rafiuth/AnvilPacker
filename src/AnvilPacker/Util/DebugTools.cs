using System;
using System.IO;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Level;

namespace AnvilPacker.Util
{
    public static class DebugTools
    {
        /// <summary> Creates a grayscale PPM file with the heightmaps of all chunks in the region. Assumes heights ranges in [0..255] </summary>
        public static void DumpHeightmaps(RegionBuffer region, string outPath = "")
        {
            var types = 
                region.ExistingChunks
                    .SelectMany(c => c.Heightmaps)
                    .Select(e => e.Key)
                    .Distinct();

            foreach (var type in types) {
                using var ppm = new DataWriter(File.Create($"{outPath}heightmap_{type}.ppm"));
                ppm.WriteBytes(Encoding.UTF8.GetBytes("P5\n512 512 255\n"));
                for (int z = 0; z < 512; z++) {
                    for (int x = 0; x < 512; x++) {
                        var chunk = region.GetChunk(x >> 4, z >> 4);
                        int height = chunk?.Heightmaps[type]?[x & 15, z & 15] ?? 0;
                        ppm.WriteByte(height);
                    }
                }
            }
        }

        public static void LoadRegion(RegionBuffer region, string filename)
        {
            using var reader = new RegionReader(filename);
            region.Load(new WorldInfo(), reader, filename);
        }
        public static void LoadRegion(RegionBuffer region, string filename, int x, int z)
        {
            using var reader = new RegionReader(File.OpenRead(filename), x, z);
            region.Load(new WorldInfo(), reader, filename);
        }
        public static void SaveRegion(RegionBuffer region, string filename)
        {
            using var writer = new RegionWriter(filename);
            region.Save(new WorldInfo(), writer);
        }
    }
}
