using System;
using System.Linq;
using System.Collections.Generic;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder
{
    public class EstimatedBlockAttribs
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public BlockPalette Palette;

        public EstimatedHeightmapAttribs HeightmapAttribs;
        public EstimatedLightAttribs LightAttribs;

        public void Estimate(RegionBuffer region)
        {
            Palette = region.Palette;
            
            HeightmapAttribs = new();
            HeightmapAttribs.Estimate(region);

            LightAttribs = new();
            //LightAttribs.Estimate(region);
        }
    }

    public class EstimatedHeightmapAttribs
    {
        public BlockPalette Palette;
        public Dictionary<string, bool[]> OpacityMap;

        public void Estimate(RegionBuffer region)
        {
            Palette = region.Palette;
            OpacityMap = new();

            foreach (var chunk in region.Chunks.ExceptNull()) {
                foreach (var (type, heights) in chunk.Heightmaps) {
                    var isOpaque = OpacityMap.GetOrAdd(type, () => new bool[Palette.Count]);

                    //TODO: handle and log invalid values
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            int y = heights[x, z] - 1;
                            var sect = chunk.GetSection(y >> 4);
                            if (sect == null) continue;

                            var blockId = sect.GetBlockId(x, y & 15, z);
                            isOpaque[blockId] = true;
                        }
                    }
                }
            }
        }
    }
    public class EstimatedLightAttribs
    {
        public BlockPalette Palette;
        public byte[] BlockEmission;
        public byte[] BlockOpacity;
        public DictionarySlim<BlockId, BitSet> TransparentSides;

        public void Estimate(RegionBuffer region)
        {
            Palette = region.Palette;
            BlockEmission = new byte[Palette.Count];
            BlockOpacity  = new byte[Palette.Count];

            EstimateEmission(region);
        }
        private void EstimateEmission(RegionBuffer region)
        {
            var hist = new int[Palette.Count * 15];

            foreach (var chunk in ChunkIterator.Create(region)) {
                var lights = chunk.BlockLight;

                if (lights == null) continue;

                int GetLight(int x, int y, int z)
                {
                    if (ChunkSection.IsCoordInside(x, y, z)) {
                        return lights[ChunkSection.GetIndex(x, y, z)];
                    }
                    return chunk.GetNeighbor(x, y, z)
                                .GetBlockLight(x & 15, y & 15, z & 15);
                }

                for (int y = 0; y < 16; y++) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            int br = GetLight(x, y, z);

                            if (br == 0) continue;

                            bool isLocalMax =
                                br >= GetLight(x - 1, y, z) && 
                                br >= GetLight(x + 1, y, z) &&
                                br >= GetLight(x, y - 1, z) && 
                                br >= GetLight(x, y + 1, z) &&
                                br >= GetLight(x, y, z - 1) && 
                                br >= GetLight(x, y, z + 1);

                            var id = chunk.GetBlockIdFast(x, y, z);
                            hist[id * 16 + br - 1] += isLocalMax ? 1 : -1;
                        }
                    }
                }
            }
            DumpEmissionHist(Palette, hist);
            ;
        }

        private void DumpEmissionHist(BlockPalette palette, int[] hist)
        {
            foreach (var (block, id) in palette.BlocksAndIds()) {
                if (hist.Skip(id * 16).Take(16).Sum() > 0) {
                    Console.WriteLine(block + " " + string.Join(' ', hist.Skip(id * 16).Take(16)));
                }
            }
        }

        private void EstimateOpacity(Chunk chunk)
        {
        }
    }
}