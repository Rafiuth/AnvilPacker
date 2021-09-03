using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Chunk bit storage used before v1.16.
    /// All elements are stored with a fixed bit count. They may occasionally span across multiple longs.
    /// </summary>
    public class PackedBitStorage : IBitStorage
    {
        public long[] Data { get; }
        public int Count { get; }
        public int BitsPerElement { get; }

        private readonly ulong mask;

        public int this[int index]
        {
            get => Get(index);
            set => Set(index, value);
        }

        /// <param name="count">Number of elements</param>
        /// <param name="bits">Bit count per element</param>
        /// <param name="data">Backing data array. If null, a new array will be allocated. </param>
        public PackedBitStorage(int count, int bits, long[]? data = null)
        {
            Ensure.That(bits < 32, "Bits per element must be less than 32");
            int dataLen = (count * bits + 63) / 64;
            if (data == null) {
                data = new long[dataLen];
            } else {
                Ensure.That(data.Length == dataLen, "Invalid length for data array");
            }
            Data = data;
            BitsPerElement = bits;
            Count = count;
            mask = (1ul << bits) - 1;
        }

        public void Unpack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor
        {
            for (int i = 0; i < Count; i++) {
                visitor.Use(i, Get(i));
            }
        }
        public void Pack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor
        {
            for (int i = 0; i < Count; i++) {
                Set(i, visitor.Create(i));
            }
        }

        /// <summary> Gets the element at the specified index. </summary>
        public int Get(int index)
        {
            Ensure.IndexValid(index, Count);
            int bitPos = index * BitsPerElement;
            int bytePos = bitPos >> 3;
            int shift = bitPos & 7;

            ulong v = ReadLE(Data, bytePos);
            return (int)((v >> shift) & mask);
        }
        /// <summary> Sets the element at the specified index. Value will be truncated to <see cref="BitsPerElement"/> bits.</summary>
        public void Set(int index, int value)
        {
            Ensure.IndexValid(index, Count);
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
            ref byte ptr = ref Mem.GetByteRef(data);
            int limit = (data.Length - 1) * 8;

            if (bytePos < limit) {
                ptr = ref Unsafe.Add(ref ptr, bytePos);
                return Mem.ReadLE<ulong>(ref ptr);
            }
            Debug.Assert(bytePos < data.Length * 8);

            ptr = ref Unsafe.Add(ref ptr, limit);
            int shift = (bytePos & 7) * 8;
            return Mem.ReadLE<ulong>(ref ptr) >> shift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLE(long[] data, int bytePos, ulong value)
        {
            ref byte ptr = ref Mem.GetByteRef(data);
            int limit = (data.Length - 1) * 8;

            if (bytePos < limit) {
                ptr = ref Unsafe.Add(ref ptr, bytePos);
                Mem.WriteLE<ulong>(ref ptr, value);
                return;
            }
            Debug.Assert(bytePos < data.Length * 8);

            ptr = ref Unsafe.Add(ref ptr, limit);
            int shift = (bytePos & 7) * 8;
            ulong mask = (1ul << shift) - 1;
            ulong existingValue = Mem.ReadLE<ulong>(ref ptr);
            value = value << shift | (existingValue & mask);

            Mem.WriteLE<ulong>(ref ptr, value);
        }
    }
}
