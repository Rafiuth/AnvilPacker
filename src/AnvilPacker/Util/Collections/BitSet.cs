using System;
using System.Numerics;

namespace AnvilPacker.Util
{
    //Custom BitArray class because BCL's just sucks.
    public sealed class BitSet
    {
        private const ulong WORD_ALL = ~0ul, WORD_NONE = 0ul;

        private ulong[] _vals;
        private int _len;

        public int Length => _len;
        public ulong[] Words => _vals;

        public BitSet(int initialCapacity = 64)
        {
            Ensure.That(initialCapacity >= 0);
            _vals = new ulong[Maths.CeilDiv(initialCapacity, 64)];
            _len = 0;
        }

        public bool this[int index]
        {
            get {
                if ((uint)index >= (uint)_len) {
                    return false;
                }
                ulong mask = 1ul << index;
                return (GetWord(index) & mask) != 0;
            }
            set {
                EnsureCapacity(index);

                ulong mask = 1ul << index;
                ref ulong w = ref GetWord(index);
                if (value) {
                    w |= mask;
                } else {
                    w &= ~mask;
                }
            }
        }
        
        /// <summary> Returns the index of the next bit set to <paramref name="value"/> after <paramref name="offset"/> (inclusive). </summary>
        public int NextIndex(bool value, int offset)
        {
            if ((uint)offset >= (uint)Length) {
                return -1;
            }
            int i = offset >> 6;
            ulong m = ~Broadcast(value);
            ulong w = (_vals[i] ^ m) & (WORD_ALL << offset);

            while (true) {
                if (w != 0) {
                    int pos = i * 64 + BitOperations.TrailingZeroCount(w);
                    return pos < Length ? pos : -1;
                }
                if (++i >= _vals.Length) break;
                w = _vals[i] ^ m;
            }
            return -1;
        }

        /// <summary> Checks whether all bits in the specified range are equal to <paramref name="value"/>. </summary>
        /// <param name="count">If negative, <see cref="Length"/> is used</param>
        public bool All(bool value, int offset = 0, int count = -1)
        {
            int end = offset + (count >= 0 ? count : _len);
            int startWord = offset >> 6;
            int lastWord = end >> 6;

            if (end >= Length) return false;

            ulong firstWordMask = WORD_ALL << offset;
            ulong lastWordMask = WORD_ALL >> -end;
            ulong flipMask = Broadcast(value);

            if (startWord == lastWord) {
                ulong mask = firstWordMask & lastWordMask;
                return (_vals[startWord] & mask) == (flipMask & mask);
            } else {
                for (int i = startWord + 1; i < lastWord; i++) {
                    if (_vals[i] != flipMask) {
                        return false;
                    }
                }
                return (_vals[startWord] & firstWordMask) == (flipMask & firstWordMask) &&
                       (_vals[lastWord ] &  lastWordMask) == (flipMask & lastWordMask);
            }
        }

        /// <summary> Sets all bits in the specified range to <paramref name="value"/>. </summary>
        public void Fill(bool value, int offset, int count)
        {
            if (value) {
                Set(offset, count);
            } else {
                Clear(offset, count);
            }
        }

        /// <summary> Sets the bit in the specified range to true. </summary>
        public void Set(int offset, int count)
        {
            EnsureCapacity(offset + count);

            int end = offset + count;
            int startWord = offset >> 6;
            int endWord = end >> 6;

            ulong firstWordMask = WORD_ALL << offset;
            ulong lastWordMask = WORD_ALL >> -end;

            if (startWord == endWord) {
                _vals[startWord] |= firstWordMask & lastWordMask;
            } else {
                for (int i = startWord + 1; i < endWord; i++) {
                    _vals[i] = WORD_ALL;
                }
                _vals[startWord] |= firstWordMask;
                _vals[endWord] |= lastWordMask;
            }
        }

        /// <summary> Sets the bit in the specified range to false. </summary>
        public void Clear(int offset, int count)
        {
            EnsureCapacity(offset + count);

            int end = offset + count;
            int startWord = offset >> 6;
            int endWord = end >> 6;

            ulong firstWordMask = WORD_ALL << offset;
            ulong lastWordMask = WORD_ALL >> -end;

            if (startWord == endWord) {
                _vals[startWord] &= ~(firstWordMask & lastWordMask);
            } else {
                for (int i = startWord + 1; i < endWord; i++) {
                    _vals[i] = 0;
                }
                _vals[startWord] &= ~firstWordMask;
                _vals[endWord] &= ~lastWordMask;
            }
        }

        /// <summary> Sets the bit at the specified position to true, and returns whether it was previously clear. </summary>
        public bool Add(int index)
        {
            EnsureCapacity(index);

            ulong mask = 1ul << index;
            ref ulong w = ref GetWord(index);
            bool wasClear = (w & mask) == 0;
            w |= mask;
            return wasClear;
        }

        /// <summary> Sets all bits to false, and the length to 0. </summary>
        public void Clear()
        {
            _vals.Clear();
            _len = 0;
        }

        private ref ulong GetWord(int index)
        {
            return ref Mem.GetRef(_vals, index >> 6);
        }
        private void EnsureCapacity(int index)
        {
            if (_len <= index) {
                _len = index + 1;

                int numWords = index >> 6;
                if ((uint)numWords >= (uint)_vals.Length) {
                    Array.Resize(ref _vals, Math.Max(numWords + 8, _vals.Length * 2));
                }
            }
        }

        private static ulong Broadcast(bool x) => x ? WORD_ALL : WORD_NONE;
    }
}