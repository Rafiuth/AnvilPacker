#nullable enable

using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    /// <summary> Represents a 16x16x16 block region. </summary>
    public class ChunkSection
    {
        //We don't use a bit array here because it just makes everything simpler and ironically faster.
        //1.8 and older versions used 8+4/16 bit array and were just fine.
        //We will never hit 64k blocks in a single section anyway.
        public BlockId[] Blocks { get; }
        /// <summary> A reference copy of the region palette. </summary>
        public BlockPalette Palette { get; set; }
        public NibbleArray? SkyLight { get; set; }
        public NibbleArray? BlockLight { get; set; }

        public Chunk Chunk { get; }
        public int Y { get; }
        public int X => Chunk.X;
        public int Z => Chunk.Z;

        public ChunkSection(Chunk chunk, int y)
        {
            Chunk = chunk;
            Y = y;
            Blocks = new BlockId[16 * 16 * 16];
            Palette = chunk.Palette;
        }

        public BlockState GetBlock(int x, int y, int z)
        {
            var id = Blocks[GetIndex(x, y, z)];
            return Palette.GetState(id);
        }
        public BlockId GetBlockId(int x, int y, int z)
        {
            return Blocks[GetIndex(x, y, z)];
        }
        public void SetBlock(int x, int y, int z, BlockState block)
        {
            var id = Palette.GetOrAddId(block);
            Blocks[GetIndex(x, y, z)] = id;
        }
        public void SetBlockId(int x, int y, int z, BlockId id)
        {
            Blocks[GetIndex(x, y, z)] = id;
        }

        public int GetSkyLight(int x, int y, int z)
        {
            if (SkyLight != null) {
                return SkyLight[GetIndex(x, y, z)];
            }
            return 0;
        }
        public void SetSkyLight(int x, int y, int z, int value)
        {
            if (SkyLight != null) {
                SkyLight[GetIndex(x, y, z)] = value;
            }
        }
        
        public int GetBlockLight(int x, int y, int z)
        {
            if (BlockLight != null) {
                return BlockLight[GetIndex(x, y, z)];
            }
            return 0;
        }
        public void SetBlockLight(int x, int y, int z, int value)
        {
            if (BlockLight != null) {
                BlockLight[GetIndex(x, y, z)] = value;
            }
        }
        
        public static int GetIndex(int x, int y, int z) => y << 8 | z << 4 | x;
    }
}
