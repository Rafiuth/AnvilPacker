using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Lighter
    {
        //https://minecraft.fandom.com/wiki/Light
        //https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1
        //https://www.seedofandromeda.com/blogs/30-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-2

        //https://github.com/Tuinity/Starlight/blob/fabric/TECHNICAL_DETAILS.md
        //https://github.com/coderbot16/flashcube/tree/trunk/lumis

        private RegionBuffer _region;
        private BlockLightInfo[] _lightAttribs;
        private LightNode[] _queue = new LightNode[32768];
        private Heightmap[] _heightmaps = new Heightmap[32 * 32];
        private short[] _emptyHeights = new short[16 * 16]; //all values set to lowest

        private SectionCache _cache = new SectionCache(0);

        public Lighter()
        {
            _emptyHeights.Fill(short.MinValue);
        }

        public void Compute(RegionBuffer region, BlockLightInfo[] blockAttribs)
        {
            _region = region;
            _lightAttribs = blockAttribs;

            ComputeHeightmaps();

            foreach (var chunk in region.ExistingChunks) {
                ComputeBlockLight(chunk);
                ComputeSkyLight(chunk);
            }
        }
        private void ComputeHeightmaps()
        {
            var opacityMap = _lightAttribs.Select(a => a.Opacity > 0).ToArray();
            foreach (var chunk in _region.ExistingChunks) {
                var heightmap = _heightmaps[(chunk.X & 31) + (chunk.Z & 31) * 32] ??= new();
                heightmap.Compute(chunk, opacityMap);
            }
        }
        private short[] GetHeights(int cx, int cz)
        {
            Heightmap heightmap = null;
            if ((cx & ~31) == _region.X && (cz & ~31) == _region.Z) {
                heightmap = _heightmaps[(cx & 31) + (cz & 31) * 32];
            }
            return heightmap?.Values ?? _emptyHeights;
        }

        private void ComputeBlockLight(Chunk chunk)
        {
            var queue = _queue;
            var attrs = _lightAttribs;
            var cache = _cache;

            foreach (var section in chunk.Sections.ExceptNull()) {
                var blocks = section.Blocks;
                var levels = GetLightData(section, LightLayer.Block);
                int sy = section.Y * 16;
                int queueTail = 0;

                for (int i = 0; i < blocks.Length; i++) {
                    int emission = attrs[blocks[i]].Emission;
                    if (emission > 0) {
                        levels[i] = emission;
                        queue[queueTail++] = new LightNode(
                            x: i >> 0 & 15,
                            z: i >> 4 & 15,
                            y: sy + (i >> 8 & 15),
                            level: emission
                        );
                    }
                }
                if (queueTail > 0) {
                    cache.SetOrigin(_region, section, LightLayer.Block);
                    PropagateLight(cache, queue, queueTail);
                }
            }
        }

        private void ComputeSkyLight(Chunk chunk)
        {
            //heightmaps point to the last transparent block
            var heights = GetHeights(chunk.X, chunk.Z);
            var heightsXN = GetHeights(chunk.X - 1, chunk.Z);
            var heightsXP = GetHeights(chunk.X + 1, chunk.Z);
            var heightsZN = GetHeights(chunk.X, chunk.Z - 1);
            var heightsZP = GetHeights(chunk.X, chunk.Z + 1);
            
            int maxHeight = chunk.MaxSectionY * 16 + 15;

            var queue = _queue;
            var queueTail = 0;
            ulong sectionMask = 0;

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    int h = heights[x + z * 16];
                    
                    //Propagate light trough column gaps, so they flow inwards like b:
                    // a)                 b)      *--- hXN or whatever
                    // FF#######FF        FF#######FF
                    // FFCBA#ABCFF  ---\  FFEDC#CDEFF
                    // FFDCB#BCDFF  ---/  FFEDC#CDEFF
                    // FFEDC#CDEFF        FFEDC#CDEFF
                    // ###########        ###########
                    //                             ^--- h (minus one)
                    // # = opaque block, hex char = light level
                    // a: queued only `h`.
                    // b: queued from `h..hMax-1`

                    int hXN = x !=  0 ? heights[(x - 1) + (z + 0) * 16] : heightsXN[15 + z * 16];
                    int hXP = x != 15 ? heights[(x + 1) + (z + 0) * 16] : heightsXP[ 0 + z * 16];
                    int hZN = z !=  0 ? heights[(x + 0) + (z - 1) * 16] : heightsZN[x + 15 * 16];
                    int hZP = z != 15 ? heights[(x + 0) + (z + 1) * 16] : heightsZP[x +  0 * 16];

                    int hMax = Max(h + 1, hXN, hXP, hZN, hZP);

                    for (int y = h; y < hMax; y++) {
                        var sides =
                            (y == h ? SideFlags.YNeg : 0) |
                            (y < hXN ? SideFlags.XNeg : 0) |
                            (y < hXP ? SideFlags.XPos : 0) |
                            (y < hZN ? SideFlags.ZNeg : 0) |
                            (y < hZP ? SideFlags.ZPos : 0);
                        //TODO: do we need to enqueue up to (inclusive) the neighbor block?
                        //It causes a significant performance drop, and results were the same in my tests.

                        Debug.Assert(sides != 0); //we are wasting time if sides == 0
                        queue[queueTail++] = new LightNode(x, y, z, 15, sides);
                    }
                    sectionMask |= CreateSectionMask((h >> 4) - chunk.MinSectionY, (hMax >> 4)  - chunk.MinSectionY);
                    FillVisibleSkyColumn(chunk, x, z, h, maxHeight);
                }
            }
            if (queueTail > 0) {
                _cache.SetOrigin(_region, chunk, LightLayer.Sky, sectionMask);
                PropagateLight(_cache, queue, queueTail);
            }

            //Fills the sky light column from minY to maxY (inclusive) with 15
            static void FillVisibleSkyColumn(Chunk chunk, int x, int z, int minY, int maxY)
            {
                int sy1 = minY >> 4;
                int sy2 = maxY >> 4;

                int xzIndex = ChunkSection.GetIndex(x, 0, z);
                int levelMask = xzIndex % 2 == 0 ? 0x0F : 0xF0;

                for (int sy = sy1; sy <= sy2; sy++) {
                    var section = chunk.GetSection(sy);
                    if (section == null) continue;

                    var levels = GetLightData(section, LightLayer.Sky);
                    var rawLevels = levels.Data;

                    int y1 = Math.Max(minY, sy * 16) & 15;
                    int y2 = Math.Min(maxY - sy * 16, 15) & 15;

                    for (int y = y1; y <= y2; y++) {
                        //int index = ChunkSection.GetIndex(x, y, z);
                        //levels[index] = 15;
                        int index = (xzIndex >> 1) + y * 128;
                        rawLevels[index] |= (byte)levelMask;
                    }
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Max(int a, int b, int c, int d, int e)
            {
                if (b > a) a = b;
                if (c > a) a = c;
                if (d > a) a = d;
                if (e > a) a = e;
                return a;
            }
            static ulong CreateSectionMask(int start, int end)
            {
                Debug.Assert(start >= 0 && start <= 63 && end >= 0 && end <= 63);

                //expand by 1 to include neighbors
                if (start > 0) start--;
                if (end < 64) end--;

                int count = end - start + 1;
                ulong mask = (1ul << count) - 1;
                return count >= 64 ? ~0ul : mask << start;
            }
        }

        private void PropagateLight(SectionCache cache, LightNode[] queue, int queueTail)
        {
            var attrs = _lightAttribs;
            var sides = BLOCK_SIDES;

            int queueHead = 0;

            while (queueHead < queueTail) {
                var node = queue[queueHead++];

                for (int i = 0; i < sides.Length; i++) {
                    if ((node.Dirs & (SideFlags)(1 << i)) == 0) continue;

                    int sx = node.X + sides[i].X;
                    int sy = node.Y + sides[i].Y;
                    int sz = node.Z + sides[i].Z;

                    int index = ChunkSection.GetIndex(sx & 15, sy & 15, sz & 15);
                    int ci = SectionCache.Index(sx, sy, sz);

                    var (chunk, levels) = cache.Entries[ci];
                    if (chunk == null) continue;

                    int currLevel = levels[index];
                    if (currLevel >= node.Level - 1) continue;

                    int opacity = attrs[chunk.Blocks[index]].Opacity;
                    int newLevel = node.Level - Math.Max(1, opacity);

                    if (newLevel > currLevel) {
                        levels[index] = newLevel;
                        queue[queueTail++] = new LightNode(sx, sy, sz, newLevel);
                    }
                }
            }
        }

        private static NibbleArray GetLightData(ChunkSection chunk, LightLayer layer)
        {
            return layer switch {
                LightLayer.Block => chunk.BlockLight ??= new(4096),
                LightLayer.Sky   => chunk.SkyLight   ??= new(4096),
                _ => throw new InvalidOperationException()
            };
        }

        private struct SectionCache
        {
            private const int COLUMN_SIZE = 64 + 2; //pad with 1 null on each endpoint
            private const int SECTION_Y_OFFSET = 32 + 1;

            //Array containing 3xNx3 neighbor chunks from the origin
            public (ChunkSection Section, NibbleArray Light)[] Entries;

            public SectionCache(int dummy) //can't define param-less ctor for struct
            {
                Entries = new (ChunkSection, NibbleArray)[3 * 3 * COLUMN_SIZE];
            }

            public void SetOrigin(RegionBuffer region, ChunkSection origin, LightLayer layer)
            {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        for (int x = -1; x <= 1; x++) {
                            var section = GetSection(region, origin.X + x, origin.Y + y, origin.Z + z);
                            var light = section == null ? null : GetLightData(section, layer);

                            int ci = Index(x << 4, (origin.Y + y) << 4, z << 4);
                            Entries[ci] = (section, light);
                        }
                    }
                }
            }
            public void SetOrigin(RegionBuffer region, Chunk chunk, LightLayer layer, ulong maskY)
            {
                Debug.Assert(chunk.MinSectionY >= -32 && chunk.MaxSectionY <= 31);

                //https://lemire.me/blog/2018/02/21/iterating-over-set-bits-quickly/
                while (maskY != 0) {
                    int y = chunk.MinSectionY + BitOperations.TrailingZeroCount(maskY);
                    maskY &= (maskY - 1); //clear lowest bit

                    for (int z = -1; z <= 1; z++) {
                        for (int x = -1; x <= 1; x++) {
                            var section = GetSection(region, chunk.X + x, y, chunk.Z + z);
                            var light = section == null ? null : GetLightData(section, layer);

                            int ci = Index(x << 4, y << 4, z << 4);
                            Entries[ci] = (section, light);
                        }
                    }
                }
            }

            private ChunkSection GetSection(RegionBuffer region, int x, int y, int z)
            {
                return region.GetChunkAbsCoords(x, z)?.GetSection(y);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Index(int x, int y, int z)
            {
                Debug.Assert(x >= -16 && y >= -512 && z >= -16 && x <= 31 && y <= 511 && z <= 31);
                //x = (x >> 4) + 1;
                //y = (y >> 4) + SECTION_Y_OFFSET;
                //z = (z >> 4) + 1;
                //return (z * 3 + y) * 3 + x;
                x >>= 4;
                y >>= 4;
                z >>= 4;
                return (y * 3 + z) * 3 + x + ((SECTION_Y_OFFSET * 3 + 1) * 3 + 1);
            }
        }


        private struct LightNode
        {
            public sbyte X, Z;  //Relative to the origin chunk
            public short Y;     //Absolute
            public byte Level;
            public SideFlags Dirs;

            public LightNode(int x, int y, int z, int level, SideFlags dirs = SideFlags.All)
            {
                X = (sbyte)x;
                Y = (short)y;
                Z = (sbyte)z;
                Level = (byte)level;
                Dirs = dirs;

                Debug.Assert(X == x && Y == y && Z == z, "LightNode too far from origin chunk");
            }

            public override string ToString() => $"Pos=[{X} {Y} {Z}] Level={Level} Dirs={Dirs} ";
        }
        private enum LightLayer
        {
            Block, Sky
        }
        [Flags]
        private enum SideFlags : byte
        {
            XNeg = 1 << 0,
            XPos = 1 << 1,
            ZNeg = 1 << 2,
            ZPos = 1 << 3,
            YNeg = 1 << 4,
            YPos = 1 << 5,
            
            AllHorz = XNeg | XPos | ZNeg | ZPos,
            AllVert = YNeg | YPos,
            All = AllHorz | AllVert
        }
        private static readonly Vec3i[] BLOCK_SIDES = {
            new(-1, 0, 0),
            new(+1, 0, 0),
            new(0, 0, -1),
            new(0, 0, +1),
            new(0, -1, 0),
            new(0, +1, 0),
        };
    }

    public struct BlockLightInfo
    {
        /// <summary> Values packed as <c>Opacity | Emission &lt;&lt; 4</c> </summary>
        public readonly byte Data;

        public int Opacity => Data & 15;
        public int Emission => Data >> 4;

        public BlockLightInfo(byte data)
        {
            Data = data;
        }
        public BlockLightInfo(int opacity, int emission)
        {
            Debug.Assert(opacity is >= 0 and <= 15 && emission is >= 0 and <= 15);
            Data = (byte)(opacity | emission << 4);
        }
        public BlockLightInfo(BlockState block)
            : this(block.LightOpacity, block.LightEmission)
        {
        }

        public override string ToString() => $"Opacity={Opacity} Emission={Emission}";
    }
}