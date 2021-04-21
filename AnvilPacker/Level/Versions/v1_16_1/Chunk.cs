using System;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Versions.v1_16_1
{
    public class Chunk : ChunkBase
    {
        public Chunk(int x, int z)
            : base(x, z)
        {
        }

        protected override ChunkSectionBase CreateSection()
        {
            throw new NotImplementedException();
        }
    }
    public class ChunkSection : ChunkSectionBase
    {
        public SparseBitStorage BlockData { get; private set; }
        public BlockPalette Palette{ get; private set; }

        public ChunkSection(BlockPalette palette, int bits)
        {
            Palette = palette;
            BlockData = new SparseBitStorage(4096, bits);
        }
        public ChunkSection(BlockPalette palette, long[] blockData)
        {
            Palette = palette;

            int bits = GetPaletteBits(palette.Count);
            BlockData = new SparseBitStorage(4096, bits, blockData);
        }

        public override BlockState GetBlock(int x, int y, int z)
        {
            int id = BlockData[GetIndex(x, y, z)];
            return Palette.GetState(id);
        }
        public override void SetBlock(int x, int y, int z, BlockState block)
        {
            //FIXME: compress palette and storage array when a block is removed
            int index = GetIndex(x, y, z);
            if (Palette.TryGetId(block, out int id)) {
                BlockData.Set(index, id);
            } else {
                AddPaletteEntry(block);
                BlockData.Set(index, Palette.GetId(block));
            }
        }

        private void AddPaletteEntry(BlockState state)
        {
            int bits = Math.Max(4, Maths.CeilLog2(Palette.Count + 1));
            if (BlockData.BitsPerElement != bits) {
                var newPalette = new BlockPalette(1 << bits);
                var newData = new SparseBitStorage(4096, bits);
                RepackData(newPalette, newData, Palette, BlockData);

                Palette = newPalette;
                BlockData = newData;
            }
            Palette.Add(state);
        }

        private static void RepackData(
            BlockPalette dstPalette, SparseBitStorage dstData, 
            BlockPalette srcPalette, SparseBitStorage srcData)
        {
            for (int i = 0; i < 4096; i++) {
                var state = srcPalette.GetState(srcData[i]);
                dstData[i] = dstPalette.GetOrAddId(state);
            }
        }

        private static int GetPaletteBits(int len)
        {
            return Math.Max(4, Maths.CeilLog2(len));
        }
    }
}