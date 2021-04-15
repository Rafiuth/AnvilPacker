using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Chunk bit storage used by v1.16 and before.
    /// All elements are stored with a fixed bit count. They may occasionally span across multiple longs.
    /// </summary>
    public class PackedBitStorage
    {
        public readonly long[] Data;

        public readonly int Count;
        public readonly int BitsPerElement;

        private readonly ulong mask;

        public int this[int index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        /// <param name="count">Number of elements</param>
        /// <param name="bits">Bit count per element</param>
        /// <param name="data">Backing data array. If null, a new array will be allocated. </param>
        public PackedBitStorage(int count, int bits, long[] data = null)
        {
            if (bits >= 32) {
                throw new ArgumentOutOfRangeException(nameof(bits), "Bits per element must be less than 32");
            }
            int dataLen = (count * bits + 63) / 64;
            if (data == null) {
                data = new long[dataLen];
            } else if (data.Length != dataLen) {
                throw new ArgumentException(nameof(data), $"Invalid length for data array");
            }
            Data = data;
            BitsPerElement = bits;
            Count = count;
            mask = (1ul << bits) - 1;
        }

        /// <summary> Gets the element at the specified index. </summary>
        public int Get(int index)
        {
            if ((uint)index >= (uint)Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            int bitPos = index * BitsPerElement;
            int bytePos = bitPos >> 3;
            int shift = bitPos & 7;

            ulong v = ReadLE(Data, bytePos);
            return (int)((v >> shift) & mask);
        }
        /// <summary> Sets the element at the specified index. Value will be truncated to <see cref="BitsPerElement"/> bits.</summary>
        public void Set(int index, int value)
        {
            if ((uint)index >= (uint)Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            int bitPos = index * BitsPerElement;
            int bytePos = bitPos >> 3;
            int shift = bitPos & 7;

            ulong v = ReadLE(Data, bytePos);
            v = (v & ~(mask << shift)) | ((ulong)value & mask) << shift;

            WriteLE(Data, bytePos, v);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadLE(long[] data, int bytePos)
        {
            ref byte ptr = ref Mem.GetByteRef(data, 0);
            int limit = (data.Length - 1) * 8;

            if (bytePos < limit) {
                return Mem.ReadLE<ulong>(ref ptr, bytePos);
            }
            Debug.Assert(bytePos < data.Length * 8);
            int shift = (bytePos & 7) * 8;
            return Mem.ReadLE<ulong>(ref ptr, limit) >> shift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLE(long[] data, int bytePos, ulong value)
        {
            ref byte ptr = ref Mem.GetByteRef(data, 0);
            int limit = (data.Length - 1) * 8;

            if (bytePos < limit) {
                Mem.WriteLE<ulong>(ref ptr, bytePos, value);
                return;
            }
            Debug.Assert(bytePos < data.Length * 8);
            int shift = (bytePos & 7) * 8;
            ulong mask = (1ul << shift) - 1;
            ulong existingValue = Mem.ReadLE<ulong>(ref ptr, limit);
            value = value << shift | (existingValue & mask);

            Mem.WriteLE<ulong>(ref ptr, limit, value);
        }
    }
}
