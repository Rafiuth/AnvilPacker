using System;
using System.Linq;
using System.Collections.Generic;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Encoder
{
    public class EstimatedHeightmapAttribs
    {
        public BlockPalette Palette;
        public Dictionary<string, bool[]> OpacityMap;

        public void Estimate(RegionBuffer region)
        {
            Palette = region.Palette;
            OpacityMap = new();

            var isDynamic = Palette.ToArray(b => !b.Block.IsKnown);
            bool anyDynamic = isDynamic.Any();

            foreach (var chunk in region.ExistingChunks) {
                foreach (var (type, heights) in chunk.Heightmaps) {
                    if (!OpacityMap.TryGetValue(type, out var isOpaque)) {
                        isOpaque = CreateOpacityMap(type);
                        OpacityMap[type] = isOpaque;
                    }
                    if (anyDynamic) {
                        EstimateFromData(chunk, heights, isOpaque, isDynamic);
                    }
                }
            }
        }

        private bool[] CreateOpacityMap(string type)
        {
            var isOpaque = new bool[Palette.Count];
            if (OpacityPredicates.TryGetValue(type, out var pred)) {
                for (int i = 0; i < isOpaque.Length; i++) {
                    var state = Palette.GetState((BlockId)i);
                    isOpaque[i] = state.Block.IsKnown && pred(state);
                }
            }
            return isOpaque;
        }

        private static void EstimateFromData(Chunk chunk, Heightmap heightmap, bool[] isOpaque, bool[] filter)
        {
            //TODO: better estimation using histograms?
            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    int y = heightmap[x, z] - 1;
                    var sect = chunk.GetSection(y >> 4);
                    if (sect == null) continue;

                    var blockId = sect.GetBlockId(x, y & 15, z);
                    if (filter[blockId]) {
                        isOpaque[blockId] = true;
                    }
                }
            }
        }

        private static readonly Dictionary<string, Predicate<BlockState>> OpacityPredicates = new() {
            { Heightmap.TYPE_LEGACY,        b => b.LightOpacity > 0 },
            { "LIGHT_BLOCKING",             b => b.LightOpacity > 0 },
            { "WORLD_SURFACE_WG",           b => b.Material != BlockMaterial.Air },
            { "WORLD_SURFACE",              b => b.Material != BlockMaterial.Air },
            { "OCEAN_FLOOR_WG",             b => b.Material.BlocksMotion },
            { "OCEAN_FLOOR",                b => b.Material.BlocksMotion },
            { "MOTION_BLOCKING",            b => b.Material.BlocksMotion || HasFluid(b) },
            { "MOTION_BLOCKING_NO_LEAVES",  b => (b.Material.BlocksMotion || HasFluid(b)) && b.Material != BlockMaterial.Leaves },
        };

        private static bool HasFluid(BlockState block)
        {
            return block.HasAttrib(BlockAttributes.HasFluid);
        }
    }
    public class EstimatedLightAttribs
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public BlockPalette Palette;
        public BlockLightInfo[] LightAttribs;

        public void Estimate(RegionBuffer region)
        {
            Palette = region.Palette;
            LightAttribs = new BlockLightInfo[Palette.Count];

            bool hasDynamicBlocks = false;

            foreach (var (state, id) in Palette.BlocksAndIds()) {
                if (state.Block.IsKnown) {
                    LightAttribs[id] = new BlockLightInfo(state);
                } else {
                    hasDynamicBlocks = true;
                }
            }

            if (hasDynamicBlocks) {
                _logger.Info($"Found dynamic blocks in {region}, estimating light attributes...");
                EstimateFromData(region);
            }
        }

        private void EstimateFromData(RegionBuffer region)
        {
            var emissionHist = new LightHist[Palette.Count];
            var opacityHist = new LightHist[Palette.Count];
            var isDynamic = Palette.ToArray(b => !b.Block.IsKnown);

            EstimateFromBlockLight(region, isDynamic, emissionHist, opacityHist);
            EstimateFromSkyLight(region, isDynamic, opacityHist);

            for (int i = 0; i < Palette.Count; i++) {
                if (isDynamic[i]) {
                    var block = Palette.GetState((BlockId)i);
                    int opacity = opacityHist[i].CalcEstimatedOpacity();
                    int emission = emissionHist[i].CalcEstimatedEmission();
                    LightAttribs[i] = new BlockLightInfo(opacity, emission);

                    if (_logger.IsTraceEnabled) {
                        _logger.Trace("Estimated opacity  for {0}: {1} hist={{{2}}}", block, opacity, opacityHist[i]);
                        _logger.Trace("Estimated emission for {0}: {1} hist={{{2}}}", block, emission, emissionHist[i]);
                    }
                }
            }
        }
        private void EstimateFromBlockLight(RegionBuffer region, bool[] blockFilter, LightHist[] emissionHist, LightHist[] opacityHist)
        {
            foreach (var chunk in ChunkIterator.Create(region)) {
                var lights = chunk.BlockLight;

                if (lights == null) continue;

                for (int y = 0; y < 16; y++) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            //opt ideas: 
                            //- unroll this loop by 2x and manually extract the nibbles.
                            var id = chunk.GetBlockIdFast(x, y, z);
                            if (!blockFilter[id]) continue;

                            int br = GetLight(x, y, z);
                            int maxNeighborBr = Max(
                                GetLight(x - 1, y, z),
                                GetLight(x + 1, y, z),
                                GetLight(x, y - 1, z),
                                GetLight(x, y + 1, z),
                                GetLight(x, y, z - 1),
                                GetLight(x, y, z + 1)
                            );
                            emissionHist[id].UpdateEmission(br, maxNeighborBr);
                            opacityHist[id].UpdateOpacity(br, maxNeighborBr, false);
                        }
                    }
                }

                int GetLight(int x, int y, int z)
                {
                    if (ChunkSection.IsCoordInside(x, y, z)) {
                        return lights[ChunkSection.GetIndex(x, y, z)];
                    }
                    return chunk.GetNeighbor(x, y, z)
                                .GetBlockLight(x & 15, y & 15, z & 15);
                }
            }
        }

        private void EstimateFromSkyLight(RegionBuffer region, bool[] blockFilter, LightHist[] opacityHist)
        {
            foreach (var chunk in region.ExistingChunks) {
                for (int z = 0; z < 16; z++) {
                    for (int x = 0; x < 16; x++) {
                        for (int sy = chunk.MaxSectionY; sy >= chunk.MinSectionY; sy--) {
                            var section = chunk.GetSection(sy);
                            var lights = section?.SkyLight;

                            if (lights == null) continue;

                            for (int y = 15; y >= 0; y--) {
                                var id = section.GetBlockId(x, y, z);
                                if (!blockFilter[id]) continue;

                                int br = GetLight(x, y, z);
                                if (br == 15) {
                                    //if br == 15, the sky should be visible in all blocks above. maxNeighborBr will endup being 15;
                                    //don't waste time fetching other neighbors in that case.
                                    //increment bin[0]
                                    opacityHist[id].UpdateOpacity(15, 15, true);
                                    continue;
                                }

                                int maxNeighborBr = Max(
                                    GetLight(x - 1, y, z),
                                    GetLight(x + 1, y, z),
                                    GetLight(x, y - 1, z),
                                    GetLight(x, y + 1, z),
                                    GetLight(x, y, z - 1),
                                    GetLight(x, y, z + 1)
                                );
                                opacityHist[id].UpdateOpacity(br, maxNeighborBr, true);

                                if (br + maxNeighborBr == 0) goto SectYLoopEnd;
                            }

                            int GetLight(int x, int y, int z)
                            {
                                if (ChunkSection.IsCoordInside(x, y, z)) {
                                    return lights[ChunkSection.GetIndex(x, y, z)];
                                }
                                return region.GetSection(section.X + (x >> 4), section.Y + (y >> 4), section.Z + (z >> 4))
                                             ?.GetSkyLight(x & 15, y & 15, z & 15) ?? 0;
                            }
                        }
                        SectYLoopEnd: ;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Max(int a, int b, int c, int d, int e, int f)
        {
            if (b > a) a = b;
            if (c > a) a = c;
            if (d > a) a = d;
            if (e > a) a = e;
            if (f > a) a = f;
            return a;
        }

        unsafe struct LightHist
        {
            public const int BIN_COUNT = 16;

            public int LightSamples, TotalBlocks;
            private fixed int _bins[BIN_COUNT];

            public Span<int> Bins => MemoryMarshal.CreateSpan(ref _bins[0], BIN_COUNT);

            public void UpdateEmission(int br, int maxNeighborBr)
            {
                Debug.Assert(br is >= 0 and <= 15 && maxNeighborBr is >= 0 and <= 15);

                if (br > 0) {
                    LightSamples++;
                    if (br >= maxNeighborBr) {
                        _bins[br]++;
                    }
                }
                TotalBlocks++;
            }
            public void UpdateOpacity(int br, int maxNeighborBr, bool forSkyLight)
            {
                Debug.Assert(br is >= 0 and <= 15 && maxNeighborBr is >= 0 and <= 15);

                if (maxNeighborBr > 0 && (br < maxNeighborBr || (forSkyLight && br == 15))) {
                    int estimatedOpacity = maxNeighborBr - br;

                    //TODO: better sky light weighting
                    int w = forSkyLight ? 16 : 1;
                    LightSamples += w;
                    _bins[estimatedOpacity] += w;
                }
                TotalBlocks++;
            }

            public int CalcEstimatedEmission()
            {
                //Sample hist 1.12.2
                //  lava     samples=88460/88461    bins=[0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 88460]  E=15
                //  air      samples=146136/4104119 bins=[0 4 4 3 1 0 0 1 3 1 0 0 1 3 1 69]     E=0
                //  obsidian samples=206/1172       bins=[0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 206]    E=15
                //  gravel   samples=317/214411     bins=[0 0 0 0 0 2 0 1 0 0 0 0 0 2 0 202]    E=15
                
                if (LightSamples == 0 || LightSamples / (double)TotalBlocks < 0.80) {
                    return 0;
                }
                int best = 0;
                int threshold = LightSamples / 12;
                for (int i = 0; i < BIN_COUNT; i++) {
                    if (_bins[i] > _bins[best] && _bins[i] > threshold) {
                        best = i;
                    }
                }
                return best;
            }
            public int CalcEstimatedOpacity()
            {
                //air         samples=3664830/7609577 bins=[3445028 215762 745 587 513 465 349 379 250 208 109 121 105 101 68 40]
                //stone       samples=116733/12549445 bins=[0 2734 2763 2985 3058 3250 3555 3567 3757 3807 4157 4517 6804 8581 6792 56406]
                //iron_ore    samples=982/114747      bins=[0 19 18 20 25 24 30 22 36 30 36 36 57 55 57 517]
                //coal_ore    samples=1964/202912     bins=[0 49 37 44 51 52 50 60 66 67 71 56 130 155 106 970]
                //gold_ore    samples=141/10663       bins=[0 4 2 2 8 5 3 2 3 4 5 9 9 6 7 72]
                //water       samples=300/1198        bins=[0 245 4 48 0 0 0 1 0 1 0 0 0 0 0 1]
                if (LightSamples == 0) {
                    return 0;
                }
                //Not sure how to improve this tbh
                int threshold = LightSamples / 12;
                for (int i = BIN_COUNT - 1; i >= 0; i--) {
                    if (_bins[i] > threshold) {
                        return i;
                    }
                }
                return 0;
            }
            
            public override string ToString()
            {
                return $"samples={LightSamples}/{TotalBlocks} bins=[{string.Join(' ', Bins.ToArray())}]";
            }
        }
    }
}