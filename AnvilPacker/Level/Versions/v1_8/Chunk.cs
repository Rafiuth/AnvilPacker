using System;

namespace AnvilPacker.Level.Versions.v1_8
{
    public class Chunk : ChunkBase
    {
        //public int[] HeightMap;
        //public byte[] Biomes;

        public Chunk(int x, int z)
            : base(x, z)
        {
        }

        protected override ChunkSectionBase CreateSection()
        {
            return new ChunkSection();
        }
    }
    public class ChunkSection : ChunkSectionBase
    {
        /// <summary> 
        /// All blocks in this section
        /// Packed as <c>blockId &lt;&lt; 4 | blockData</c>
        /// Indexed as <c>y * 256 + z * 16 + x</c>
        /// </summary>
        public readonly ushort[] Blocks = new ushort[4096];

        public override BlockState GetBlock(int x, int y, int z)
        {
            return LegacyBlocks.GetStateFromId(Blocks[GetIndex(x, y, z)]);
        }
        public override void SetBlock(int x, int y, int z, BlockState block)
        {
            Blocks[GetIndex(x, y, z)] = (ushort)LegacyBlocks.GetStateId(block);
        }
    }
}