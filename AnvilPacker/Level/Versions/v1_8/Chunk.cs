using System;

namespace AnvilPacker.Level.Versions.v1_8
{
    public class Chunk : ChunkBase
    {
        public int[] HeightMap;
        public byte[] Biomes;

        public Chunk(int x, int y)
            : base(x, y, LBlockState.Air)
        {
        }

        protected override IChunkSection CreateSection()
        {
            return new ChunkSection();
        }
    }
    public class ChunkSection : IChunkSection
    {
        /// <summary> 
        /// All blocks in this section
        /// Packed as <c>blockId &lt;&lt; 4 | blockData</c>
        /// Indexed as <c>y * 256 + z * 16 + x</c>
        /// </summary>
        public readonly ushort[] BlockData = new ushort[4096];
        public readonly LightState[] LightData = new LightState[4096];

        public IBlockState GetBlock(int x, int y, int z)
        {
            return LBlockState.Get(BlockData[GetIndex(x, y, z)]);
        }
        public void SetBlock(int x, int y, int z, IBlockState block)
        {
            BlockData[GetIndex(x, y, z)] = (ushort)block.Id;
        }

        private static int GetIndex(int x, int y, int z) => y << 8 | z << 4 | x;
    }
}