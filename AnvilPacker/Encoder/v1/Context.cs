using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    public class Context
    {
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
        public const int MAX_SAMPLES = 4;
        /// <summary> Fixed array for the 4 samples. </summary>
        public fixed ushort s[MAX_SAMPLES];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSlot(int bits)
        {
            ref byte data = ref Unsafe.As<ushort, byte>(ref s[0]);
            ulong w0 = Mem.ReadLE<ulong>(ref data, 0);

            ulong hash = Hash(w0);
            return (int)(hash >> (64 - bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Hash(ulong x)
        {
            //https://github.com/Cyan4973/xxHash/blob/7bf3d9f331d0b7d0f5856ae1894e0314e2b304c2/xxhash.h#L2284
            ulong acc = 0x27D4EB2F165667C5ul;
            acc += x * 0xC2B2AE3D27D4EB4Ful;
            acc = BitOperations.RotateLeft(acc, 31);
            acc *= 0x9E3779B185EBCA87ul;
            return acc;
        }
    }
}