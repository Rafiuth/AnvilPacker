using System;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Versions.v1_16_1
{
    public class Chunk : ChunkBase
    {
        public Chunk(int x, int y)
            : base(x, y, MBlockState.Air)
        {
        }

        protected override IChunkSection CreateSection()
        {
            throw new NotImplementedException();
        }
    }
    public class ChunkSection : IChunkSection
    {
        public readonly SparseBitStorage BlockData;
        public readonly MBlockPalette Palette;

        public ChunkSection(MBlockPalette palette, int bits)
        {
            Palette = palette;
            BlockData = new SparseBitStorage(4096, bits);
        }
        public ChunkSection(MBlockPalette palette, long[] blockData)
        {
            Palette = palette;

            int bits = Math.Max(4, Maths.CeilLog2(palette.Count));
            BlockData = new SparseBitStorage(4096, bits, blockData);
        }

        public IBlockState GetBlock(int x, int y, int z)
        {
            int id = BlockData[GetIndex(x, y, z)];
            return Palette.Get(id);
        }
        public void SetBlock(int x, int y, int z, IBlockState block)
        {
            throw new NotImplementedException();
        }

        private static int GetIndex(int x, int y, int z) => y << 8 | z << 4 | x;
    }
}