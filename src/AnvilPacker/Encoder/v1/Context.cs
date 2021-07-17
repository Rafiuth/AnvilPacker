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
        private int[] _freqs;
        private int _hits;

        private NzCoder _nz = new();

        public Context(BlockPalette palette)
        {
            _palette = new BlockId[palette.Count];
            for (int i = 0; i < _palette.Length; i++) {
                _palette[i] = (BlockId)i;
            }
            _freqs = new int[palette.Count];
        }

        public void Write(ArithmEncoder ac, BlockId id)
        {
            var palette = _palette;
            int index = FindIndex(palette, id);
            Update(palette, index);
            _nz.Write(ac, index, palette.Length - 1);
        }

        public BlockId Read(ArithmDecoder ac)
        {
            var palette = _palette;
            int index = _nz.Read(ac, palette.Length - 1);
            var id = palette[index];
            Update(palette, index);
            return id;
        }

        [MethodImpl(Inline)]
        private int FindIndex(BlockId[] palette, BlockId value)
        {
            //Try a simple loop first to avoid the overhead of calling IndexOf().
            for (int i = 0; i < palette.Length && i < 4; i++) {
                if (palette[i] == value) return i;
            }
            //JIT produces less asm when passing bounds explicitly.
            return Array.IndexOf(palette, value, 0, palette.Length);
        }

        [MethodImpl(Inline)]
        private void Update(BlockId[] palette, int index)
        {
            int[] freqs = _freqs;
            var curr = palette[index];
            freqs[curr]++;
            _hits++;

            if (index == 0) return;

            //Move frequent symbols to front using a slightly modified of the count method:
            //https://en.wikipedia.org/wiki/Self-organizing_list#Count_method
            int currFreq = freqs[curr] + (1 + _hits / 32);

            while (index > 0) {
                var prev = palette[index - 1];
                if (freqs[prev] > currFreq) break;
                palette[index] = prev;
                index--;
            }
            palette[index] = curr;
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