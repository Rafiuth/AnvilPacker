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
        public int[] Freq;
        public int Hits;

        public NzCoder Nz = new NzCoder();

        public Context(BlockPalette palette)
        {
            Palette = new BlockId[palette.Count];
            for (int i = 0; i < Palette.Length; i++) {
                Palette[i] = (BlockId)i;
            }
            Freq = new int[palette.Count];
        }

        public int PredictForward(BlockId value)
        {
            int index = FindIndex(Palette, value);
            Update(Palette, index);
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindIndex(BlockId[] palette, BlockId value)
        {
            //Try a simple loop first to avoid IndexOf() overhead.
            for (int i = 0; i < palette.Length && i < 8; i++) {
                if (palette[i] == value) return i;
            }
            //This call won't be inlined. JIT produces less asm when passing bounds explicitly.
            return Array.IndexOf(palette, value, 0, palette.Length);
        }
        public BlockId PredictBackward(int delta)
        {
            var id = Palette[delta];
            Update(Palette, delta);
            return id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Update(BlockId[] palette, int index)
        {
            var curr = palette[index];
            if (index != 0) {
                //Move frequent symbols to front
                int currWeight = Weight(curr);
                int j = index;

                while (j > 0) {
                    var prev = palette[j - 1];
                    if (Weight(prev) > currWeight) break;
                    palette[j] = prev;
                    j--;
                }
                palette[j] = curr;
            }
            Freq[curr]++;
            Hits++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Weight(int id)
        {
            return Freq[id] / (1 + Hits / 64);
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