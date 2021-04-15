using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class CodingUnit
    {
        public readonly Vec3i Pos;
        public readonly int Size;

        /// <summary> 
        /// The array of blocks in this unit, each entry corresponds to a block state in the region palette. <see cref="RegionEncoder._palette"/> <para/>
        /// YZX ordered. Helper function <see cref="GetIndex(int, int, int)"/> can be used to the calc index.
        /// </summary>
        public ushort[] Blocks;
        /// <summary> Used blocks, not an actual palette. </summary>
        public ushort[] Palette;

        /// <summary> Relative positions for context samples. </summary>
        public Vec3i[] ContextNeighbors;
        /// <summary> Possible block contexts in this unit. </summary>
        public Context[] Contexts;
        /// <summary> Context for each block. </summary>
        public ushort[] BlockContexts;

        public CodingUnit(Vec3i pos, int size)
        {
            Pos = pos;
            Size = size;
            int len = size * size * size;
            Blocks = new ushort[len];
            BlockContexts = new ushort[len];
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
    public class Context
    {
        //used to calculate the actual palette
        public BitArray BlockUsed;
        public int PaletteLen = 0;

        public ushort[] Palette;
        public NzContext Nz = new NzContext();

        public int PredictForward(ushort value)
        {
            int index = Array.IndexOf(Palette, value);
            MoveToFront(index);

            return index;
        }
        public ushort PredictBackward(int delta)
        {
            ushort id = Palette[delta];
            MoveToFront(delta);

            return id;
        }

        private void MoveToFront(int index)
        {
            if (index != 0) {
                ushort actual = Palette[index];
                Array.Copy(Palette, 0, Palette, 1, index);
                Palette[0] = actual;
            }
        }
    }
    public unsafe struct ContextKey
    {
        /// <summary> Fixed array for the 4 samples. </summary>
        public fixed ushort s[4];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSlot(int bits)
        {
            ref byte data = ref Unsafe.As<ushort, byte>(ref s[0]);
            ulong w0 = Mem.ReadLE<ulong>(ref data, 0);
            ulong hash = Avalanche(w0);
            return (int)(hash >> (64 - bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Avalanche(ulong x)
        {
            //https://github.com/Cyan4973/xxHash/blob/7bf3d9f331d0b7d0f5856ae1894e0314e2b304c2/xxhash.h#L3244
            x ^= x >> 37;
            x *= 0x165667919E3779F9UL;
            x ^= x >> 32;
            return x;
        }
    }
}
