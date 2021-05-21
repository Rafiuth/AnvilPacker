using System;
using System.Runtime.CompilerServices;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    public sealed class Context
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

        public void Write(ArithmEncoder ac, BlockId value)
        {
            int index = FindIndex(Palette, value);
            Update(Palette, index);
            Nz.Write(ac, index, 0, Palette.Length - 1);
        }

        public BlockId Read(ArithmDecoder ac)
        {
            int delta = Nz.Read(ac, 0, Palette.Length - 1);
            var id = Palette[delta];
            Update(Palette, delta);
            return id;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindIndex(BlockId[] palette, BlockId value)
        {
            //Try a simple loop first to avoid IndexOf() overhead.
            for (int i = 0; i < palette.Length && i < 4; i++) {
                if (palette[i] == value) return i;
            }
            //This call won't be inlined. JIT produces less asm when passing bounds explicitly.
            return Array.IndexOf(palette, value, 0, palette.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Update(BlockId[] palette, int index)
        {
            var curr = palette[index];
            if (index != 0) {
                //Move frequent symbols to front
                int currWeight = Weight(curr) + 1;
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
            return Freq[id] / (1 + Hits / 32);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSlot(ulong key, int bits)
        {
            return (int)(Hash(key) >> (64 - bits));
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