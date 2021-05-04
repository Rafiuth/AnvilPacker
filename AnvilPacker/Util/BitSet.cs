using System;
using System.Numerics;

namespace AnvilPacker.Util
{
    //Custom BitArray class because BCL's just sucks.
    public sealed class BitSet
    {
        private readonly ulong[] _vals; //note: value of trailing bits (i >= Length) are undefined.
        public int Length { get; }

        public BitSet(int length)
        {
            if (length <= 0) {
                //0-length sets are not supported because it eliminates the need to check for it.
                throw new ArgumentException("BitSet length must be a non-zero positive integer.");
            }
            _vals = new ulong[Maths.CeilDiv(length, 64)];
            Length = length;
        }

        public bool this[int index]
        {
            get {
                ulong mask = 1ul << index;
                return (_vals[index >> 6] & mask) != 0;
            }
            set {
                ulong mask = 1ul << index;
                ref ulong w = ref _vals[index >> 6];
                if (value) {
                    w |= mask;
                } else {
                    w &= ~mask;
                }
            }
        }
/* TODO
        public int FirstIndexOf(bool value)
        {
            ulong negMask = Broadcast(!value);

            for (int i = 0; i < _vals.Length; i++) {
                ulong x = _vals[i];

                if (x != negMask) {
                    if (i == _vals.Length - 1) {
                        x &= FinalMask() ^ negMask;
                    }
                    int index = BitOperations.TrailingZeroCount(x ^ negMask);
                    return i * 64 + index;
                }
            }
            return -1;
        }*/

        /// <summary> Checks whether all elements are equal to the specified parameter. </summary>
        public bool All(bool value)
        {
            ulong mask = Broadcast(value);
            for (int i = 0; i < _vals.Length - 1; i++) {
                if (_vals[i] != mask) {
                    return false;
                }
            }
            return _vals[^1] == (mask & FinalMask());
        }

        public void Fill(bool value)
        {
            _vals.Fill(Broadcast(value));
        }
        private static ulong Broadcast(bool x) => x ? ~0ul : 0ul;
        // Returns the mask that excludes the unused bits in _vals[^1]
        private ulong FinalMask() => ~0ul >> (_vals.Length * 64 - Length);
    }
}