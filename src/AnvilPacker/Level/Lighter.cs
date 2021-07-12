using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Lighter
    {
        //https://minecraft.fandom.com/wiki/Light
        //https://www.seedofandromeda.com/blogs/29-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-1
        //https://www.seedofandromeda.com/blogs/30-fast-flood-fill-lighting-in-a-blocky-voxel-game-pt-2

        private Queue<LightNode> _skyQueue = new(1024);
        private Queue<LightNode> _blockQueue = new(1024);
        private RegionBuffer _region;
        private BlockLightInfo[] _lightAttribs;
        private Heightmap[] _heightmaps = new Heightmap[32 * 32];

        public Lighter()
        {
            for (int i = 0; i < _heightmaps.Length; i++) {
                _heightmaps[i] = new Heightmap();
            }
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
            var computer = new HeightmapComputer(_region, "LIGHT_OPAQUE", opacityMap);
            foreach (var chunk in _region.ExistingChunks) {
                computer.Compute(chunk, GetHeightmap(chunk));
            }
        }
        private Heightmap GetHeightmap(Chunk chunk)
        {
            return _heightmaps[(chunk.X & 31) + (chunk.Z & 31) * 32];
        }

        private void ComputeBlockLight(Chunk chunk)
        {
            var queue = _blockQueue;
            var attrs = _lightAttribs;

            foreach (var section in chunk.Sections.ExceptNull()) {
                var blocks = section.Blocks;
                var levels = GetLightData(section, LightLayer.Block);

                for (int i = 0; i < blocks.Length; i++) {
                    int emission = attrs[blocks[i]].Emission;
                    if (emission > 0) {
                        levels[i] = emission;
                        queue.Enqueue(new LightNode(i, emission));
                    }
                }
                PropagateLight(section, queue, LightLayer.Block);
            }
        }
        private void ComputeSkyLight(Chunk chunk)
        {
            var firstSection = chunk.Sections.FirstOrDefault(c => c != null);
            if (firstSection == null) return;

            var queue = _skyQueue;
            var attrs = _lightAttribs;
            var heights = GetHeightmap(chunk);

            int minHeight = chunk.MinSectionY * 16;
            int maxHeight = chunk.MaxSectionY * 16 + 15;

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    int h = heights[x, z]; //last transparent block

                    int hn = h; //highest neighbor
                    hn = Math.Max(hn, GetHeight(x - 1, z));
                    hn = Math.Max(hn, GetHeight(x + 1, z));
                    hn = Math.Max(hn, GetHeight(x, z - 1));
                    hn = Math.Max(hn, GetHeight(x, z + 1));

                    for (int y = h; y <= hn; y++) {
                        queue.Enqueue(new LightNode(x, y, z, 15));
                    }
                    FillVisibleColumn(x, z, h, maxHeight);
                }
            }
            PropagateLight(firstSection, queue, LightLayer.Sky);

            int GetHeight(int x, int z)
            {
                if (ChunkSection.IsCoordInside(x, 0, z)) {
                    return heights[x, z];
                } else {
                    int cx = (chunk.X & 31) + (x >> 4);
                    int cz = (chunk.Z & 31) + (z >> 4);
                    
                    if ((uint)(cx | cz) < 32) {
                        return _heightmaps[cx + cz * 32][x & 15, z & 15];
                    }
                    return minHeight;
                }
            }
            void FillVisibleColumn(int x, int z, int minY, int maxY)
            {
                int sy1 = minY >> 4;
                int sy2 = maxY >> 4;

                for (int sy = sy1; sy <= sy2; sy++) {
                    var section = chunk.GetSection(sy);
                    if (section == null) continue;

                    var levels = GetLightData(section, LightLayer.Sky);

                    int y1 = Math.Max(minY, sy * 16) & 15;
                    int y2 = Math.Min(maxY - sy * 16, 15) & 15;

                    for (int y = y1; y <= y2; y++) {
                        int index = ChunkSection.GetIndex(x, y, z);
                        levels[index] = 15;
                    }
                }
            }
        }

        private void PropagateLight(ChunkSection chunk, Queue<LightNode> queue, LightLayer layer)
        {
            var attrs = _lightAttribs;
            var levels = GetLightData(chunk, layer);
            int cx = chunk.X & 31;
            int cy = chunk.Y;
            int cz = chunk.Z & 31;

            while (queue.TryDequeue(out var node)) {
                foreach (var dir in BLOCK_SIDES) {
                    int sx = node.X + dir.X;
                    int sy = node.Y + dir.Y;
                    int sz = node.Z + dir.Z;

                    var sChunk = chunk;
                    var sLevels = levels;
                    if (!ChunkSection.IsCoordInside(sx, sy, sz)) {
                        sChunk = GetSection(cx + (sx >> 4), cy + (sy >> 4), cz + (sz >> 4));
                        if (sChunk == null) continue;

                        sLevels = GetLightData(sChunk, layer);
                    }
                    int index = ChunkSection.GetIndex(sx & 15, sy & 15, sz & 15);

                    int opacity = attrs[sChunk.Blocks[index]].Opacity;
                    int newLevel = node.Level - Math.Max(1, opacity);
                    int currLevel = sLevels[index];

                    if (newLevel > currLevel) {
                        sLevels[index] = newLevel;
                        queue.Enqueue(new LightNode(sx, sy, sz, newLevel));
                    }
                }
            }
        }


        private Chunk GetChunk(int x, int z)
        {
            return _region.GetChunk(x, z);
        }
        private ChunkSection GetSection(int x, int y, int z)
        {
            return GetChunk(x, z)?.GetSection(y);
        }

        private NibbleArray GetLightData(ChunkSection chunk, LightLayer layer)
        {
            return layer switch {
                LightLayer.Block => chunk.BlockLight ??= new(4096),
                LightLayer.Sky   => chunk.SkyLight ??= new(4096),
                _ => throw new InvalidOperationException()
            };
        }

        private static readonly Vec3i[] BLOCK_SIDES = {
            new(-1, 0, 0),
            new(+1, 0, 0),
            new(0, -1, 0),
            new(0, +1, 0),
            new(0, 0, -1),
            new(0, 0, +1),
        };

        private struct LightNode
        {
            /// <summary> Coords relative to the origin chunk section. </summary>
            public sbyte X, Z;
            public short Y;
            public byte Level;
            //TODO: maybe pack this struct into a bitfield to fit into 4 bytes

            public LightNode(int x, int y, int z, int level)
            {
                Debug.Assert(x == (sbyte)x && y == (short)y && z == (sbyte)z, "LightNode too far from origin chunk");
                X = (sbyte)x;
                Y = (short)y;
                Z = (sbyte)z;
                Level = (byte)level;
            }
            public LightNode(int index, int level)
            {
                X = (sbyte)(index >> 0 & 15);
                Y = (short)(index >> 8 & 15);
                Z = (sbyte)(index >> 4 & 15);
                Level = (byte)level;
            }

            public override string ToString() => $"{X} {Y} {Z}: {Level}";
        }
        private enum LightLayer
        {
            Block, Sky
        }
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