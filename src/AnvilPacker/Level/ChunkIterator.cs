using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    /// <summary>
    /// Provides an efficient way to iterate through 16x16x16 chunks in a world region.
    /// </summary>
    public class ChunkIterator
    {
        private static readonly Chunk EmptyChunk = new Chunk(0, 0, new BlockPalette());
        private static readonly ChunkSection EmptySection = EmptyChunk.GetOrCreateSection(0);

        public RegionBuffer Region;
        public ChunkSection Section;
        /// <summary> An array containing 3x3x3 (27) neighbor chunks of the current section. Lazily populated.</summary>
        public ChunkSection[] NeighborCache = new ChunkSection[3 * 3 * 3];

        public Chunk Chunk => Section.Chunk;
        public BlockPalette Palette => Section.Palette;
        public BlockId[] Blocks => Section.Blocks;
        public NibbleArray SkyLight => Section.SkyLight;
        public NibbleArray BlockLight => Section.BlockLight;
        public int X => Section.X;
        public int Y => Section.Y;
        public int Z => Section.Z;

        /// <summary> Creates a stream with the chunk sections in specified region, in YZX order. </summary>
        public static IEnumerable<ChunkIterator> Create(RegionBuffer region)
        {
            var itr = new ChunkIterator();
            foreach (var (section, _) in GetSections(region, 1)) {
                itr.SetCurrent(region, section);
                yield return itr;
            }
        }

        /// <summary> 
        /// Creates a stream with the chunk sections in specified region, 
        /// visiting one block layer at a time, in YZX order.
        /// The Y in the tuple is the block coordinate within the section (modulo 16).
        /// Users of this function may iterate the chunk blocks with a 16x16 ZX loop.
        /// </summary>
        public static IEnumerable<(ChunkIterator Chunk, int Y)> CreateLayered(RegionBuffer region)
        {
            var itr = new ChunkIterator();
            foreach (var (section, y) in GetSections(region, 16)) {
                itr.SetCurrent(region, section);
                yield return (itr, y);
            }
        }

        /// <summary> Creates a stream with the chunk sections in specified region, in YZX order. </summary>
        public static IEnumerable<ChunkSection> GetSections(RegionBuffer region)
        {
            return GetSections(region, 1).Select(v => v.Section);
        }

        private static IEnumerable<(ChunkSection Section, int LayerY)> GetSections(RegionBuffer region, int layers)
        {
            int minY = 0, maxY = 15;
            int x = 0, z = 0;

            yLoop:
            for (int y = minY; y <= maxY; y++) {
                for (int by = 0; by < layers; by++) {
                    for (; z < region.Size; z++) {
                        for (; x < region.Size; x++) {
                            var chunk = region.GetChunk(x, z);
                            var section = chunk?.GetSection(y);
                            if (section != null) {
                                //Reset Y loop if this chunk is taller, but keep XZ
                                if (chunk.MinSectionY < minY || chunk.MaxSectionY > maxY) {
                                    minY = Math.Min(minY, chunk.MinSectionY);
                                    maxY = Math.Max(maxY, chunk.MaxSectionY);
                                    goto yLoop;
                                }
                                yield return (section, by);
                            }
                        }
                        x = 0;
                    }
                    z = 0;
                }
            }
        }

        public void SetCurrent(RegionBuffer region, ChunkSection section)
        {
            Region = region;
            Section = section;
            NeighborCache.Clear();
        }

        /// <summary> Gets the block at the specified position. </summary>
        /// <remarks> 
        /// Coordinates are relative to the current section. The supported range is [-16..31]. <br/> 
        /// If the coordinate is expected to be within the current chunk, use <see cref="GetBlockFast(int, int, int)"/> instead. <br/>
        /// </remarks>
        public BlockState GetBlock(int x, int y, int z)
        {
            if ((uint)(x | y | z) < 16) {
                return Section.GetBlock(x, y, z);
            }
            return GetInterBlock(x, y, z);
        }
        public BlockId GetBlockId(int x, int y, int z)
        {
            if ((uint)(x | y | z) < 16) {
                return Section.GetBlockId(x, y, z);
            }
            return GetInterBlockId(x, y, z);
        }

        private BlockState GetInterBlock(int x, int y, int z)
        {
            var neighbor = GetNeighbor(x, y, z);
            return neighbor.GetBlock(x & 15, y & 15, z & 15);
        }
        private BlockId GetInterBlockId(int x, int y, int z)
        {
            var neighbor = GetNeighbor(x, y, z);
            return neighbor.GetBlockId(x & 15, y & 15, z & 15);
        }

        /// <summary> Gets the block at the specified position, assumming it is inside the current section. </summary>
        public BlockState GetBlockFast(int x, int y, int z)
        {
            return Section.GetBlock(x, y, z);
        }
        public BlockId GetBlockIdFast(int x, int y, int z)
        {
            return Section.GetBlockId(x, y, z);
        }

        /// <summary> Sets a block at the specified position, assumming it is inside the current section. </summary>
        public void SetBlock(int x, int y, int z, BlockState block)
        {
            Section.SetBlock(x, y, z, block);
        }
        public void SetBlockId(int x, int y, int z, BlockId id)
        {
            Section.SetBlockId(x, y, z, id);
        }

        /// <summary> Returns the neighbor section at given block coordinates. </summary>
        public ChunkSection GetNeighbor(int x, int y, int z)
        {
            int index = GetNeighborIndex(x, y, z);
            return NeighborCache[index] ?? InitNeighbor(x, y, z, index);
        }
        private ChunkSection InitNeighbor(int x, int y, int z, int index)
        {
            int sx = X + (x >> 4) - Region.X;
            int sy = Y + (y >> 4);
            int sz = Z + (z >> 4) - Region.Z;

            var neighbor = Region.GetSection(sx, sy, sz) ?? EmptySection;
            NeighborCache[index] = neighbor;
            return neighbor;
        }
        private static int GetNeighborIndex(int x, int y, int z)
        {
            //map coords from [-16..31] to [0..2]
            x = (x + 16) >> 4;
            y = (y + 16) >> 4;
            z = (z + 16) >> 4;

            if ((uint)x < 3 && (uint)y < 3 && (uint)z < 3) {
                return (y * 3 + z) * 3 + x;
            }
            throw new NotImplementedException("Cannot get block farther than 16 blocks from the origin section.");
        }
    }
}