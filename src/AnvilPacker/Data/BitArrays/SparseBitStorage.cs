using System;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Chunk bit storage used by v1.16 and after
    /// All elements are stored with a fixed bit count, but their bits will never span across multiple 
    /// longs. Any remaining space is left unused.
    /// </summary>
    //this is about 1.3x slower than PackedBitStorage (random access)
    //using a div by const thing like Minecraft does might improve it (see libdivide)

    // bit[i] = (data[i / elemBits] >> (i % elemBits)) & mask
    public class SparseBitStorage : IBitStorage
    {
        public long[] Data { get; }
        public int Count { get; }
        public int BitsPerElement { get; }

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
            Ensure.That(bits < 32, "Bits per element must be less than 32");
            int valsPerLong = 64 / bits;

            int dataLen = Maths.CeilDiv(count, valsPerLong);
            if (data == null) {
                data = new long[dataLen];
            } else {
                Ensure.That(data.Length == dataLen, "Invalid length for data array");
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
            Ensure.IndexValid(index, Count);
            int dataIndex = index / valuesPerLong;
            int shift = (index - dataIndex * valuesPerLong) * BitsPerElement;

            ref long v = ref Mem.GetRef(Data, dataIndex);
            return (int)((v >> shift) & mask);
        }
        /// <summary> Sets the element at the specified index. Value will be truncated to <see cref="BitsPerElement"/> bits.</summary>
        public void Set(int index, int value)
        {
            Ensure.IndexValid(index, Count);
            int dataIndex = index / valuesPerLong;
            int shift = (index - dataIndex * valuesPerLong) * BitsPerElement;

            ref long v = ref Mem.GetRef(Data, dataIndex);
            v = (v & ~(mask << shift)) | (value & mask) << shift;
        }

        public void Unpack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor
        {
            int dataPos = 0;
            
            for (int i = 0; i < Count; i += valuesPerLong) {
                int valCount = Math.Min(Count - i, valuesPerLong);
                long vals = Data[dataPos++];

                for (int j = 0; j < valCount; j++) {
                    visitor.Use(i + j, (int)(vals & mask));
                    vals >>= BitsPerElement;
                }
            }
        }
        public void Pack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor
        {
            int dataPos = 0;

            for (int i = 0; i < Count; i += valuesPerLong) {
                int valCount = Math.Min(Count - i, valuesPerLong);
                long vals = 0;

                for (int j = 0; j < valCount; j++) {
                    long val = visitor.Create(i + j) & mask;
                    vals |= val << (j * BitsPerElement);
                }
                Data[dataPos++] = vals;
            }
        }
    }
}
