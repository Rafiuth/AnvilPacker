using System;
using System.Collections;
using System.Numerics;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class CodingUnit
    {
        public readonly Vec3i Pos;
        public readonly int Size;

        /// <summary> 
        /// The array of blocks in this unit, each entry corresponds to a block state in the region palette. <see cref="RegionSplitter._palette"/> <para/>
        /// YZX ordered. Helper function <see cref="GetIndex(int, int, int)"/> can be used to the calc index.
        /// </summary>
        public ushort[] Blocks;
        /// <summary> Used blocks, not an actual palette. </summary>
        public ushort[] Palette;

        public CodingUnit(Vec3i pos, int size)
        {
            Pos = pos;
            Size = size;
            int len = size * size * size;
            Blocks = new ushort[len];
        }

        public ushort GetBlock(int x, int y, int z)
        {
            return Blocks[GetIndex(x, y, z)];
        }
        public void SetBlock(int x, int y, int z, ushort id)
        {
            Blocks[GetIndex(x, y, z)] = id;
        }

        public int GetIndex(int x, int y, int z)
        {
            return y * (Size * Size) +
                   z * Size +
                   x;
        }
    }
}
