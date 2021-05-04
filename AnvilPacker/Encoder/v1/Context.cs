using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    public class Context
    {
        public BlockId[] Palette;
        public NzCoder Nz = new NzCoder();

        public Context(BlockPalette palette)
        {
            Palette = new BlockId[palette.Count];
            for (int i = 0; i < Palette.Length; i++) {
                Palette[i] = (BlockId)i;
            }
        }

        public int PredictForward(BlockId value)
        {
            if (Palette[0] == value) {
                //avoid IndexOf() overhead as this should be very likely
                return 0;
            }
            int index = Array.IndexOf(Palette, value);
            MoveToFront(index);
            return index;
        }
        public BlockId PredictBackward(int delta)
        {
            var id = Palette[delta];
            MoveToFront(delta);
            return id;
        }

        private void MoveToFront(int index)
        {
            if (index != 0) {
                var actual = Palette[index];
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
        public readonly int GetSlot(int bits)
        {
            ref byte data = ref Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in s[0]));
            ulong w0 = Mem.ReadLE<ulong>(ref data, 0);
            ulong hash = Hash(w0);
            return (int)(hash >> (64 - bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Hash(ulong x)
        {
            //https://lemire.me/blog/2018/08/15/fast-strongly-universal-64-bit-hashing-everywhere/
            const ulong a = 0x65D200CE55B19AD8ul;
            const ulong b = 0x4F2162926E40C299ul;
            const ulong c = 0x162DD799029970F8ul;

            uint lo = (uint)x;
            uint hi = (uint)(x >> 32);
            return a * lo + b * hi + c;
        }
    }
}