using System;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Chunk bit storage used by v1.16.1 and after
    /// All elements are stored with a fixed bit count, but their bits will never span across multiple 
    /// longs. Any remaining space is left unused.
    /// </summary>
    //TODO: optimize
    //this is about 1.3x slower than PackedBitStorage
    //using a div by const thing like Minecraft does might improve it (see libdivide)
    public class SparseBitStorage
    {
        public readonly long[] Data;

        public readonly int Count;
        public readonly int BitsPerElement;

        private readonly int valuesPerLong;
        private readonly long mask;

        public int this[int index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        /// <param name="count">Number of elements</param>
        /// <param name="bits">Bit count per element</param>
        /// <param name="data">Backing data array. If null, a new array will be allocated. </param>
        public SparseBitStorage(int count, int bits, long[] data = null)
        {
            if (bits >= 32) {
                throw new ArgumentOutOfRangeException(nameof(bits), "Bits per element must be less than 32");
            }
            int valsPerLong = 64 / bits;

            int dataLen = Maths.CeilDiv(count, valsPerLong);
            if (data == null) {
                data = new long[dataLen];
            } else if (data.Length != dataLen) {
                throw new ArgumentException($"Invalid length for data array.", nameof(data));
            }
            Data = data;
            BitsPerElement = bits;
            Count = count;
            valuesPerLong = valsPerLong;

            mask = (1u << bits) - 1;
        }

        /// <summary> Gets the element at the specified index. </summary>
        public int Get(int index)
        {
            if ((uint)index >= (uint)Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            int dataIndex = index / valuesPerLong;
            int shift = (index - dataIndex * valuesPerLong) * BitsPerElement;

            ref long v = ref Mem.GetRef(Data, dataIndex);
            return (int)((v >> shift) & mask);
        }
        /// <summary> Sets the element at the specified index. Value will be truncated to <see cref="BitsPerElement"/> bits.</summary>
        public void Set(int index, int value)
        {
            if ((uint)index >= (uint)Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            int dataIndex = index / valuesPerLong;
            int shift = (index - dataIndex * valuesPerLong) * BitsPerElement;

            ref long v = ref Mem.GetRef(Data, dataIndex);
            v = (v & ~(mask << shift)) | (value & mask) << shift;
        }

        /// <summary> 
        /// Copies the elements from this storage to <paramref name="dst"/>.
        /// Elements are truncated to the dest storage bit size.
        /// An exception is thrown if the dest storage count is smaller than this instance's.
        /// </summary>
        public void CopyTo(SparseBitStorage dst)
        {
            Ensure.That(dst.Count >= Count, "Destination must be at least as large as source.");

            if (BitsPerElement == dst.BitsPerElement) {
                int count = Math.Min(Data.Length, dst.Data.Length);
                Data.AsSpan(0, count).CopyTo(dst.Data);
                return;
            }
            for (int i = 0; i < Data.Length; i++) {
                int bitPos = i * valuesPerLong;
                int elemCount = Math.Min(Count - bitPos, valuesPerLong);
                long elems = Data[i];

                for (int j = 0; j < elemCount; j++) {
                    dst[bitPos + j] = (int)(elems & mask);
                    elems >>= BitsPerElement;
                }
            }
        }
    }
}
