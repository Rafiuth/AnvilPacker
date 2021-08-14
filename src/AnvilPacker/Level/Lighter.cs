#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AnvilPacker.Data;
using AnvilPacker.Level.Physics;
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

        private readonly RegionBuffer _region;
        private readonly BlockLightInfo[] _lightAttribs;
        private readonly Heightmap[] _heightmaps = new Heightmap[32 * 32];
        private readonly short[] _emptyHeights = new short[16 * 16]; //all values set to lowest
        private readonly bool _enqueueBorders;

        private LightQueue _queue = new();
        private SectionCache _cache = new(0);

        public Lighter(RegionBuffer region, BlockLightInfo[] blockAttribs, bool enqueueBorders = false)
        {
            _emptyHeights.Fill(short.MinValue);
            _region = region;
            _lightAttribs = blockAttribs;
            _enqueueBorders = enqueueBorders;
        }

        public void Compute()
        {
            ComputeHeightmaps();

            foreach (var chunk in _region.ExistingChunks) {
                ComputeBlockLight(chunk);
                ComputeSkyLight(chunk);
            }
        }
        private void ComputeHeightmaps()
        {
            var opacityMap = _lightAttribs.Select(a => a.Opacity > 0).ToArray();
            foreach (var chunk in _region.ExistingChunks) {
                var heightmap = _heightmaps[RegionBuffer.GetChunkIndex(chunk)] ??= new();
                heightmap.Compute(chunk, opacityMap);
            }
        }
        private short[] GetHeights(int cx, int cz)
        {
            Heightmap? heightmap = null;
            if (_region.IsChunkInside(cx, cz)) {
                heightmap = _heightmaps[RegionBuffer.GetChunkIndex(cx, cz)];
            }
            return heightmap?.Values ?? _emptyHeights;
        }

        private void ComputeBlockLight(Chunk chunk)
        {
            var attrs = _lightAttribs;
            var cache = _cache;

            foreach (var section in chunk.Sections.ExceptNull()) {
                var blocks = section.Blocks;
                var levels = section.GetOrCreateLightData(LightLayer.Block);
                int sy = section.Y * 16;
                var queue = _queue.Cleared();

                for (int i = 0; i < blocks.Length; i++) {
                    var blockId = blocks[i];
                    int emission = attrs[blockId].Emission;

                    if (emission > 0) {
                        levels[i] = emission;
                        queue.Enqueue(
                            x: i >> 0 & 15,
                            z: i >> 4 & 15,
                            y: sy + (i >> 8 & 15),
                            level: emission
                        );
                    }
                }

                if (IsRegionBorder(section.X, section.Z)) {
                    EnqueueBorders(section, queue, LightLayer.Block);
                }
                if (!queue.IsEmpty) {
                    cache.SetOrigin(_region, section, LightLayer.Block);
                    PropagateLight(cache, queue);
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

            var queue = _queue.Cleared();
            int minY = int.MaxValue, maxY = int.MinValue;

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    int h = heights[x + z * 16];

                    //Propagate light trough column gaps, so they flow inwards like b:
                    // a)             b)      *--- hXN or whatever
                    // FF#######FF    FF#######FF
                    // FFCBA#ABCFF    FFEDC#CDEFF
                    // FFDCB#BCDFF    FFEDC#CDEFF
                    // FFEDC#CDEFF    FFEDC#CDEFF
                    // ###########    ###########
                    //                         ^--- h (minus one)
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
                            (y == h  ? Direction.YNeg : 0) |
                            (y < hXN ? Direction.XNeg : 0) |
                            (y < hXP ? Direction.XPos : 0) |
                            (y < hZN ? Direction.ZNeg : 0) |
                            (y < hZP ? Direction.ZPos : 0);
                        //TODO: do we need to enqueue up to (inclusive) the neighbor block?
                        //It causes a significant performance drop, and results were the same in my tests.

                        Debug.Assert(sides != 0); //we are wasting time if sides == 0
                        queue.Enqueue(x, y, z, 15, sides);
                    }
                    FillVisibleSkyColumn(chunk, x, z, h, maxHeight);
                    minY = Math.Min(minY, h);
                    maxY = Math.Max(maxY, hMax);
                }
            }
            if (IsRegionBorder(chunk.X, chunk.Z)) {
                EnqueueBorders(chunk, queue, LightLayer.Sky);
            }
            if (!queue.IsEmpty) {
                _cache.SetOrigin(_region, chunk, LightLayer.Sky, minY >> 4, maxY >> 4);
                PropagateLight(_cache, queue);
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

                    var levels = section.GetOrCreateLightData(LightLayer.Sky);
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
        }

        private void EnqueueBorders(ChunkSection section, LightQueue queue, LightLayer layer)
        {
            var data = section.GetLightData(layer);
            int sy = section.Y * 16;

            if (data != null) {
                int sx = section.X & 31;
                int sz = section.Z & 31;

                if (sx ==  0) EnqueuePlaneX( 0, Direction.XPos);
                if (sx == 31) EnqueuePlaneX(15, Direction.XNeg);
                if (sz ==  0) EnqueuePlaneZ( 0, Direction.ZPos);
                if (sz == 31) EnqueuePlaneZ(15, Direction.ZNeg);
            }

            void EnqueuePlaneX(int xo, Direction sides)
            {
                for (int by = 0; by < 16; by++) {
                    for (int bz = 0; bz < 16; bz++) {
                        int level = data[ChunkSection.GetIndex(xo, by, bz)];
                        if (level > 0) {
                            queue.Enqueue(xo, sy + by, bz, level, sides);
                        }
                    }
                }
            }
            void EnqueuePlaneZ(int zo, Direction sides)
            {
                for (int by = 0; by < 16; by++) {
                    for (int bx = 0; bx < 16; bx++) {
                        int level = data[ChunkSection.GetIndex(bx, by, zo)];
                        if (level > 0) {
                            queue.Enqueue(bx, sy + by, zo, level, sides);
                        }
                    }
                }
            }
        }
        private void EnqueueBorders(Chunk chunk, LightQueue queue, LightLayer layer)
        {
            foreach (var section in chunk.Sections) {
                if (section != null) {
                    EnqueueBorders(section, queue, layer);
                }
            }
        }
        private bool IsRegionBorder(int cx, int cz)
        {
            return _enqueueBorders && (cx == 0 || cx == 31 || cz == 0 || cz == 31);
        }

        private void PropagateLight(SectionCache cache, LightQueue queue)
        {
            var attrs = _lightAttribs;
            var sides = Directions.Normals;

            while (!queue.IsEmpty) {
                ref var node = ref queue.Dequeue();

                for (int i = 0; i < sides.Length; i++) {
                    if ((node.Dirs & (Direction)(1 << i)) == 0) continue;

                    int sx = node.X + sides[i].X;
                    int sy = node.Y + sides[i].Y;
                    int sz = node.Z + sides[i].Z;

                    int ci = SectionCache.Index(sx, sy, sz);
                    var (blocks, levels) = cache.Entries[ci];
                    if (blocks == null) continue;

                    int index = ChunkSection.GetIndex(sx & 15, sy & 15, sz & 15);
                    int currLevel = NibbleArray.Get(levels, index);
                    if (currLevel >= node.Level - 1) continue;

                    int opacity = attrs[blocks[index]].Opacity;
                    int newLevel = node.Level - Math.Max(1, opacity);

                    if (newLevel > currLevel) {
                        NibbleArray.Set(levels, index, newLevel);
                        queue.Enqueue(sx, sy, sz, newLevel);
                    }
                }
            }
        }

        //Perf notes:
        //- Using a priority queue (so that highest levels are propagated first) 
        //only saved about 0.0016%-0.28% iterations in my tests.
        //- Class is slightly faster than struct.
        private class LightQueue
        {
            private LightNode[] _arr = new LightNode[32768];
            private int _head, _tail;

            public bool IsEmpty => _head >= _tail;

            /// <summary> Adds a new node at the head of the queue. </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Enqueue(int x, int y, int z, int level, Direction dirs = Direction.All)
            {
                _arr[_tail++].Set(x, y, z, level, dirs);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref LightNode Dequeue()
            {
                //Checking if we need to shift here instead
                //of Enqueue() will save up to 6 checks
                if (_tail + 8 > _arr.Length) {
                    Shift();
                }
                return ref _arr[_head++];
            }

            public LightQueue Cleared()
            {
                _head = _tail = 0;
                return this;
            }

            private void Shift()
            {
                var newArr = _arr;
                int count = _tail - _head;

                if (count > _arr.Length - 4096) {
                    //Expand a bit if we can't get away with just a shift.
                    //This should be very rare, if it even happens at all.
                    //This array will be discarded once the queue is copied again (we're a struct)
                    newArr = new LightNode[_arr.Length + 4096];
                }
                Array.Copy(_arr, _head, newArr, 0, count);

                _arr = newArr;
                _head = 0;
                _tail = count;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        private struct LightNode
        {
            public sbyte X, Z;  //Relative to the origin chunk
            public short Y;     //Absolute
            public byte Level;
            public Direction Dirs;

            //Not using ctors because jit will create a copy before the array store
            public void Set(int x, int y, int z, int level, Direction dirs = Direction.All)
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

        private struct SectionCache
        {
            private const int COLUMN_SIZE = 64 + 2; //pad with 1 null on each endpoint
            private const int SECTION_Y_OFFSET = 32 + 1;

            //Array containing 3xNx3 neighbor chunks from the origin
            public (BlockId[] Blocks, byte[] Light)[] Entries;

            public SectionCache(int dummy) //can't define param-less ctor for struct
            {
                Entries = new (BlockId[], byte[])[3 * 3 * COLUMN_SIZE];
            }

            public void SetOrigin(RegionBuffer region, ChunkSection origin, LightLayer layer)
            {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        for (int x = -1; x <= 1; x++) {
                            var section = GetSection(region, origin.X + x, origin.Y + y, origin.Z + z);
                            int ci = Index(x << 4, (origin.Y + y) << 4, z << 4);

                            Entries[ci] = section == null
                                ? default
                                : (section.Blocks, section.GetOrCreateLightData(layer).Data);

                        }
                    }
                }
            }
            public void SetOrigin(RegionBuffer region, Chunk chunk, LightLayer layer, int minY, int maxY)
            {
                Debug.Assert(chunk.MinSectionY >= -32 && chunk.MaxSectionY <= 31);

                minY--; maxY++; //include +1 chunk on top and bottom
                for (int y = minY; y <= maxY; y++) {
                    for (int z = -1; z <= 1; z++) {
                        for (int x = -1; x <= 1; x++) {
                            var section = GetSection(region, chunk.X + x, y, chunk.Z + z);
                            int ci = Index(x << 4, y << 4, z << 4);

                            Entries[ci] = section == null
                                ? default
                                : (section.Blocks, section.GetOrCreateLightData(layer).Data);
                        }
                    }
                }
            }

            private ChunkSection? GetSection(RegionBuffer region, int x, int y, int z)
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
    }

    public enum LightLayer
    {
        Block, Sky
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