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
        private readonly BlockLightInfo[] _blockAttribs;
        private readonly VoxelShape[] _occlusionShapes;  //impl assumes that entries are set to VoxelShape.Empty when attrib.UseShapeForOcclusion == false
        private readonly BlockId _emptyShapeId; //index of a VoxelShape.Empty in _occlusionShapes
        private readonly Heightmap[] _heightmaps = new Heightmap[32 * 32];
        private readonly short[] _emptyHeights = new short[16 * 16]; //all values set to lowest
        private readonly bool _enqueueBorders;

        private readonly LightQueue _queue = new();
        private readonly SectionCache _sectionCache = new(0); //struct with a class field, fine with readonly?

        public Lighter(RegionBuffer region, Encoder.EstimatedLightAttribs estimAttribs, bool enqueueBorders = false)
            : this(region, estimAttribs.LightAttribs, estimAttribs.OcclusionShapes, enqueueBorders)
        {
        }
        public Lighter(RegionBuffer region, BlockLightInfo[] lightAttribs, VoxelShape[] occlusionShapes, bool enqueueBorders = false)
        {
            int paletteLen = region.Palette.Count;
            Ensure.That(lightAttribs.Length == paletteLen);
            Ensure.That(occlusionShapes.Length == paletteLen);

            _emptyHeights.Fill(short.MinValue);
            _region = region;
            _blockAttribs = lightAttribs;
            _occlusionShapes = occlusionShapes;
            _enqueueBorders = enqueueBorders;

            int emptyShapeIdx = Array.IndexOf(_occlusionShapes, VoxelShape.Empty);
            if (emptyShapeIdx >= 0) {
                _emptyShapeId = (BlockId)emptyShapeIdx;
            } else {
                //oh shit, how did we end up here?
                _emptyShapeId = (BlockId)_occlusionShapes.Length;
                _occlusionShapes = _occlusionShapes.Append(VoxelShape.Empty).ToArray();
                _blockAttribs = _blockAttribs.Append(new BlockLightInfo(0, 0, false)).ToArray();
            }
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
            var opacityMap = _blockAttribs.Select(a => a.Opacity > 0 || a.UseShapeForOcclusion).ToArray();
            
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
            var blockAttribs = _blockAttribs;

            foreach (var section in chunk.Sections.ExceptNull()) {
                var blocks = section.Blocks;
                var levels = section.GetOrCreateLightData(LightLayer.Block);
                int sy = section.Y * 16;
                var queue = _queue.Cleared();

                for (int i = 0; i < blocks.Length; i++) {
                    var blockId = blocks[i];
                    var attribs = blockAttribs[blockId];

                    if (attribs.Emission <= 0) continue;

                    levels[i] = attribs.Emission;
                    queue.Enqueue(
                        x: i >> 0 & 15,
                        z: i >> 4 & 15,
                        y: sy + (i >> 8 & 15),
                        level: attribs.Emission,
                        dirs: Direction.All,
                        useShapeForOcclusion: attribs.UseShapeForOcclusion,
                        block: blockId
                    );
                }
                if (IsRegionBorder(section.X, section.Z)) {
                    EnqueueBorders(section, LightLayer.Block);
                }
                if (!queue.IsEmpty) {
                    _sectionCache.SetOrigin(_region, section, LightLayer.Block);
                    PropagateLight();
                }
            }
        }
        private void ComputeSkyLight(Chunk chunk)
        {
            var queue = _queue.Cleared();
            var blockAttribs = _blockAttribs;
            var blockShapes = _occlusionShapes;
            var emptyShapeId = _emptyShapeId;

            int minY = int.MaxValue, maxY = int.MinValue;

            //heightmaps point to the last transparent or 
            //non-shape aware (UseShapeForOcclusion == false) block.
            var heights = GetHeights(chunk.X, chunk.Z);
            var heightsXN = GetHeights(chunk.X - 1, chunk.Z);
            var heightsXP = GetHeights(chunk.X + 1, chunk.Z);
            var heightsZN = GetHeights(chunk.X, chunk.Z - 1);
            var heightsZP = GetHeights(chunk.X, chunk.Z + 1);

            int maxHeight = chunk.MaxSectionY * 16 + 15;

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    //Propagate light through column gaps, so they flow inwards like b:
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
                    int h = heights[x + z * 16];

                    int hXN = x !=  0 ? heights[(x - 1) + (z + 0) * 16] : heightsXN[15 + z * 16];
                    int hXP = x != 15 ? heights[(x + 1) + (z + 0) * 16] : heightsXP[ 0 + z * 16];
                    int hZN = z !=  0 ? heights[(x + 0) + (z - 1) * 16] : heightsZN[x + 15 * 16];
                    int hZP = z != 15 ? heights[(x + 0) + (z + 1) * 16] : heightsZP[x +  0 * 16];

                    int hMax = Max(h + 1, hXN, hXP, hZN, hZP);

                    var blockAtH    = chunk.GetBlockId(x, h,     z, emptyShapeId);
                    var blockAtH_m1 = chunk.GetBlockId(x, h - 1, z, emptyShapeId);

                    for (int y = hMax - 1; y >= h; y--) {
                        var dirs =
                            (y == h  ? Direction.YNeg : 0) |
                            (y < hXN ? Direction.XNeg : 0) |
                            (y < hXP ? Direction.XPos : 0) |
                            (y < hZN ? Direction.ZNeg : 0) |
                            (y < hZP ? Direction.ZPos : 0);
                        //TODO: should the neighbor comparasion above be inclusive?
                        //It causes a significant performance drop, and results were the same in my tests.
                        queue.Enqueue(
                            x, y, z, level: 15,
                            dirs,
                            useShapeForOcclusion: false,
                            block: blockAtH //don't care about the actual block, shape just need to be empty.
                        );
                        Debug.Assert(dirs != 0); //we are wasting time if sides == 0
                    }
                    //also enqueue h-1 if light can go inside it
                    if (blockAttribs[blockAtH_m1].UseShapeForOcclusion &&
                        !VoxelShape.MergedFacesOccludes(VoxelShape.Empty, blockShapes[blockAtH_m1], Direction.YNeg)) 
                    {
                        h--;
                        queue.Enqueue(
                            x, h, z, level: 15,
                            dirs: Direction.All & ~Direction.YPos,
                            useShapeForOcclusion: true,
                            blockAtH_m1
                        );
                    }
                    FillVisibleSkyColumn(chunk, x, z, h, maxHeight);
                    minY = Math.Min(minY, h);
                    maxY = Math.Max(maxY, hMax);
                }
            }
            if (IsRegionBorder(chunk.X, chunk.Z)) {
                EnqueueBorders(chunk, LightLayer.Sky);
            }
            if (!queue.IsEmpty) {
                _sectionCache.SetOrigin(_region, chunk, LightLayer.Sky, minY >> 4, maxY >> 4);
                PropagateLight();
            }

            //Fills the sky light column from minY to maxY (inclusive) with 15
            static void FillVisibleSkyColumn(Chunk chunk, int x, int z, int minY, int maxY)
            {
                int xzIndex = ChunkSection.GetIndex(x, 0, z);
                int levelMask = xzIndex % 2 == 0 ? 0x0F : 0xF0;

                int sy1 = minY >> 4;
                int sy2 = maxY >> 4;

                for (int sy = sy1; sy <= sy2; sy++) {
                    var section = chunk.GetSection(sy);
                    if (section == null) continue;

                    var levels = section.GetOrCreateLightData(LightLayer.Sky);
                    var rawLevels = levels.Data;

                    int y1 = Math.Max(minY, sy * 16) & 15;
                    int y2 = Math.Min(maxY - sy * 16, 15);

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

        private void EnqueueBorders(ChunkSection section, LightLayer layer)
        {
            var queue = _queue;
            var attribs = _blockAttribs;
            var data = section.GetLightData(layer);
            var blocks = section.Blocks;
            int sy = section.Y * 16;

            if (data != null) {
                int sx = section.X & 31;
                int sz = section.Z & 31;

                if (sx ==  0) EnqueuePlane(Direction.XPos, true,  0, 0);
                if (sx == 31) EnqueuePlane(Direction.XNeg, true,  15, 0);
                if (sz ==  0) EnqueuePlane(Direction.ZPos, false, 0, 0);
                if (sz == 31) EnqueuePlane(Direction.ZNeg, false, 0, 15);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] //const prop
            void EnqueuePlane(Direction side, bool axisX, int xo, int zo)
            {
                for (int by = 0; by < 16; by++) {
                    for (int bh = 0; bh < 16; bh++) {
                        int x = axisX ? xo : bh;
                        int z = axisX ? bh : zo;

                        int index = ChunkSection.GetIndex(x, by, z);
                        int level = data[index];
                        if (level <= 0) continue;

                        var blockId = blocks[index];
                        queue.Enqueue(
                            x, sy + by, z,
                            level, side,
                            attribs[blockId].UseShapeForOcclusion,
                            blockId
                        );
                    }
                }
            }
        }
        private void EnqueueBorders(Chunk chunk, LightLayer layer)
        {
            foreach (var section in chunk.Sections) {
                if (section != null) {
                    EnqueueBorders(section, layer);
                }
            }
        }
        private bool IsRegionBorder(int cx, int cz)
        {
            return _enqueueBorders && (cx == 0 || cx == 31 || cz == 0 || cz == 31);
        }

        private void PropagateLight()
        {
            var queue = _queue;
            var sectionCache = _sectionCache;
            var blockAttribs = _blockAttribs;
            var blockShapes = _occlusionShapes;
            var sides = Directions.Normals;

            while (!queue.IsEmpty) {
                ref var node = ref queue.Dequeue();

                for (int i = 0; i < sides.Length; i++) {
                    var dir = (Direction)(1 << i);
                    if (!node.HasDir(dir)) continue;

                    int sx = node.X + sides[i].X;
                    int sy = node.Y + sides[i].Y;
                    int sz = node.Z + sides[i].Z;

                    var (blocks, levels) = sectionCache.GetEntry(sx, sy, sz);
                    if (blocks == null) continue; //empty section

                    int index = ChunkSection.GetIndex(sx & 15, sy & 15, sz & 15);
                    int currLevel = NibbleArray.Get(levels, index);

                    //avoid block access if the level is already 
                    //greater than what we could have set
                    if (currLevel >= node.Level - 1) continue;

                    var blockId = blocks[index];
                    var attrs = blockAttribs[blockId];
                    int newLevel = node.Level - Math.Max(1, attrs.Opacity);
                    if (newLevel <= currLevel) continue;

                    if (attrs.UseShapeForOcclusion || node.UseShapeForOcclusion) {
                        var shapeFrom = blockShapes[node.Block];
                        var shapeTo = blockShapes[blockId];
                        if (VoxelShape.MergedFacesOccludes(shapeFrom, shapeTo, dir)) continue;
                    }

                    NibbleArray.Set(levels, index, newLevel);
                    //only enqueue if it can propagate further
                    if (newLevel <= 1) continue;

                    queue.Enqueue(
                        sx, sy, sz, newLevel,
                        Direction.All & ~dir.Opposite(),
                        attrs.UseShapeForOcclusion,
                        blockId
                    );
                }
                queue.EnsureCapacity();
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
            public void Enqueue(int x, int y, int z, int level, Direction dirs, bool useShapeForOcclusion, BlockId block)
            {
                _arr[_tail++].Set(x, y, z, level, dirs, useShapeForOcclusion, block);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref LightNode Dequeue()
            {
                return ref _arr[_head++];
            }

            public LightQueue Cleared()
            {
                _head = _tail = 0;
                return this;
            }

            public void EnsureCapacity()
            {
                //The queue is large enough so that this is rare,
                //but it could be worth using a normal circular buffer...
                if (_tail + 8 > _arr.Length) {
                    ShiftOrResize();
                }
            }

            private void ShiftOrResize()
            {
                var newArr = _arr;
                int count = _tail - _head;

                if (count > _arr.Length - 4096) {
                    //Expand a bit if we can't get away with just a shift.
                    //This should be very rare, if it even happens at all.
                    newArr = new LightNode[_arr.Length + 4096];
                }
                Array.Copy(_arr, _head, newArr, 0, count);

                _arr = newArr;
                _head = 0;
                _tail = count;
            }
        }

        private struct LightNode
        {
            public sbyte X, Z;  //Relative to the origin chunk
            public short Y;     //Absolute
            public byte Level;
            public byte Flags;  //[0..5]: Dirs, 6: UseShapeForOcclusion
            public BlockId Block; //Only used when this or any neighbor UseShapeForOcclusion==True. 
            //Size = 8 bytes

            public bool UseShapeForOcclusion => (Flags & (1 << 6)) != 0;
            public bool HasDir(Direction dir)
            {
                return (Flags & (int)dir) != 0;
            }

            //Not using ctors because jit won't eliminate the copy before the array store
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(int x, int y, int z, int level, Direction dirs, bool useShapeForOcclusion, BlockId block)
            {
                X = (sbyte)x;
                Y = (short)y;
                Z = (sbyte)z;
                Level = (byte)level;
                Flags = (byte)(
                    (int)dirs | 
                    (Unsafe.As<bool, byte>(ref useShapeForOcclusion) << 6)
                );
                Block = block;
                Debug.Assert(X == x && Y == y && Z == z, "LightNode too far from origin chunk");
            }

            public override string ToString()
            {
                return $"Pos=[{X} {Y} {Z}] Level={Level} " +
                       $"Dirs={(Direction)(Flags & (int)Direction.All)} " +
                       $"Flags={(Flags >> 6).ToString("X")}";
            }
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
                    UpdateChunkLayer(region, layer, origin.X, origin.Z, origin.Y + y);
                }
            }
            public void SetOrigin(RegionBuffer region, Chunk chunk, LightLayer layer, int minY, int maxY)
            {
                Debug.Assert(chunk.MinSectionY >= -32 && chunk.MaxSectionY <= 31);

                for (int y = minY - 1; y <= maxY + 1; y++) {
                    UpdateChunkLayer(region, layer, chunk.X, chunk.Z, y);
                }
            }

            private void UpdateChunkLayer(RegionBuffer region, LightLayer lightLayer, int ox, int oz, int y)
            {
                for (int z = -1; z <= 1; z++) {
                    for (int x = -1; x <= 1; x++) {
                        var section = region.GetChunkAbsCoords(ox + x, oz + z)
                                            ?.GetSection(y);

                        ref var entry = ref GetEntry(x << 4, y << 4, z << 4);

                        if (section != null) {
                            var blocks = section.Blocks;
                            var lightData = section.GetOrCreateLightData(lightLayer).Data;
                            entry = (blocks, lightData);
                        } else {
                            entry = default;
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref (BlockId[] Blocks, byte[] Light) GetEntry(int x, int y, int z)
            {
                Debug.Assert(x >= -16 && y >= -512 && z >= -16 && x <= 31 && y <= 511 && z <= 31);
                //x = (x >> 4) + 1;
                //y = (y >> 4) + SECTION_Y_OFFSET;
                //z = (z >> 4) + 1;
                //int index = (z * 3 + y) * 3 + x;
                x >>= 4;
                y >>= 4;
                z >>= 4;
                const int CENTER_OFFSET = (SECTION_Y_OFFSET * 3 + 1) * 3 + 1;
                int index = (y * 3 + z) * 3 + x + CENTER_OFFSET;
                return ref Entries[index];
            }
        }
    }

    public enum LightLayer
    {
        Block, Sky
    }
    public struct BlockLightInfo
    {
        /// <summary> Values packed as <c>Opacity | Emission &lt;&lt; 12 | UseShapeForOcclusion &lt;&lt; 4 </c> </summary>
        public readonly ushort Data;

        public int Opacity => Data & 15;
        public int Emission => Data >> 12;
        public bool UseShapeForOcclusion => (Data & 0x10) != 0;

        public BlockLightInfo(int opacity, int emission, bool useShapeForOcclusion)
        {
            Debug.Assert(opacity is >= 0 and <= 15 && emission is >= 0 and <= 15);
            Data = (ushort)(opacity | emission << 12 | (useShapeForOcclusion ? 0x10 : 0));
        }
        public BlockLightInfo(BlockState block)
            : this(
                block.LightOpacity, 
                block.LightEmission,
                block.HasAttrib(BlockAttributes.Opaque | BlockAttributes.UseShapeForOcclusion)
            )
        {
        }
        public override string ToString() 
        {
            return $"Opacity={Opacity} Emission={Emission}" +
                   (UseShapeForOcclusion ?" (shape aware)" : "");
        }
    }
}