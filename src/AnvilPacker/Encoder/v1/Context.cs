using System;
using System.Runtime.CompilerServices;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;

namespace AnvilPacker.Encoder.v1
{
    public sealed class Context
    {
        const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;

        private BlockId[] _palette;
        private int[] _freq;
        private int _hits;

        private NzCoder _nz = new();

        public Context(BlockPalette palette)
        {
            _palette = new BlockId[palette.Count];
            for (int i = 0; i < _palette.Length; i++) {
                _palette[i] = (BlockId)i;
            }
            _freq = new int[palette.Count];
        }

        public void Write(ArithmEncoder ac, BlockId value)
        {
            int index = FindIndex(_palette, value);
            Update(_palette, index);
            _nz.Write(ac, index, _palette.Length - 1);
        }

        public BlockId Read(ArithmDecoder ac)
        {
            int delta = _nz.Read(ac, _palette.Length - 1);
            var id = _palette[delta];
            Update(_palette, delta);
            return id;
        }

        [MethodImpl(Inline)]
        private int FindIndex(BlockId[] palette, BlockId value)
        {
            //Try a simple loop first to avoid IndexOf() overhead.
            for (int i = 0; i < palette.Length && i < 8; i++) {
                if (palette[i] == value) return i;
            }
            //This call won't be inlined. JIT produces less asm when passing bounds explicitly.
            return Array.IndexOf(palette, value, 0, palette.Length);
        }

        [MethodImpl(Inline)]
        private void Update(BlockId[] palette, int index)
        {
            var curr = palette[index];
            _freq[curr]++;
            _hits++;

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
        }
        [MethodImpl(Inline)]
        private int Weight(int id)
        {
            return _freq[id] / (1 + _hits / 32);
        }


        [MethodImpl(Inline)]
        public static int GetSlot(ulong key, int bits)
        {
            return (int)(Hash(key) >> (64 - bits));
        }
        [MethodImpl(Inline)]
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