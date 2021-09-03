using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    /// <summary> Provides high performance, low level memory related functions. </summary>
    public static unsafe class Mem
    {
        private const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
        
        /// <summary> Reinterprets the given value to <typeparamref name="TDst"/> </summary>
        [MethodImpl(Inline)]
        public static TDst As<TSrc, TDst>(TSrc val)
        {
            return Unsafe.As<TSrc, TDst>(ref val);
        }

        /// <summary> Reinterprets the given readonly reference to a <typeparamref name="TDst"/> pointer. </summary>
        [MethodImpl(Inline)]
        public static ref TDst AsRef<TSrc, TDst>(in TSrc ptr)
        {
            return ref Unsafe.As<TSrc, TDst>(ref Unsafe.AsRef(in ptr));
        }

        /// <summary> Creates a span over the specified reference. </summary>
        /// <param name="length">Number of <typeparamref name="TSrc"/> elements in <paramref name="ptr"/>. </param>
        [MethodImpl(Inline)]
        public static Span<TDst> CreateSpan<TSrc, TDst>(ref TSrc ptr, int length)
            where TSrc : unmanaged
            where TDst : unmanaged
        {
            var span = MemoryMarshal.CreateSpan(ref ptr, length);
            return MemoryMarshal.Cast<TSrc, TDst>(span);
        }
        [MethodImpl(Inline)]
        public static Span<T> CreateSpan<T>(ref T ptr, int length)
        {
            return MemoryMarshal.CreateSpan(ref ptr, length);
        }

        /// <summary> Returns a reference to the n-th byte of the array. </summary>
        [MethodImpl(Inline)]
        public static ref byte GetByteRef<T>(T[] array)
        {
            return ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(array));
        }
        /// <summary> Returns a reference to the n-th byte of the span. </summary>
        [MethodImpl(Inline)]
        public static ref byte GetByteRef<T>(ReadOnlySpan<T> span)
        {
            return ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        }
        /// <summary> Returns a reference to the n-th byte of the span. </summary>
        [MethodImpl(Inline)]
        public static ref byte GetByteRef<T>(Span<T> span)
        {
            return ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span));
        }

        /// <summary> Returns a reference to the n-th element of the array. </summary>
        [MethodImpl(Inline)]
        public static ref T GetRef<T>(T[] arr, int elemOffset = 0)
        {
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), elemOffset);
        }

        /// <summary> Returns a reference to the n-th element of the span. </summary>
        [MethodImpl(Inline)]
        public static ref T GetRef<T>(ReadOnlySpan<T> span, int elemOffset = 0)
        {
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(span), elemOffset);
        }
        /// <summary> Returns a reference to the n-th element of the span. </summary>
        [MethodImpl(Inline)]
        public static ref T GetRef<T>(Span<T> span, int elemOffset = 0)
        {
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(span), elemOffset);
        }

        #region Read/Write<T> overloads

        /// <summary> Reads a <typeparamref name="T"/> element from the array, without bounds check, in the platform's native byte order. </summary>
        [MethodImpl(Inline)]
        public static T Read<T>(byte[] buf, int bytePos) where T : unmanaged
        {
            return Read<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }
        /// <summary> Reads a <typeparamref name="T"/> element from the array, without bounds check, in little-endian byte order. </summary>
        [MethodImpl(Inline)]
        public static T ReadLE<T>(byte[] buf, int bytePos) where T : unmanaged
        {
            return ReadLE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }
        /// <summary> Reads a <typeparamref name="T"/> element from the array, without bounds check, in big-endian byte order. </summary>
        [MethodImpl(Inline)]
        public static T ReadBE<T>(byte[] buf, int bytePos) where T : unmanaged
        {
            return ReadBE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }

        [MethodImpl(Inline)]
        public static T Read<T>(ReadOnlySpan<byte> buf, int bytePos) where T : unmanaged
        {
            return Read<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }
        [MethodImpl(Inline)]
        public static T ReadLE<T>(ReadOnlySpan<byte> buf, int bytePos) where T : unmanaged
        {
            return ReadLE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }
        [MethodImpl(Inline)]
        public static T ReadBE<T>(ReadOnlySpan<byte> buf, int bytePos) where T : unmanaged
        {
            return ReadBE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos));
        }

        [MethodImpl(Inline)]
        public static T Read<T>(byte* ptr) where T : unmanaged
        {
            return Unsafe.ReadUnaligned<T>(ptr);
        }
        [MethodImpl(Inline)]
        public static T ReadLE<T>(byte* ptr) where T : unmanaged
        {
            var value = Unsafe.ReadUnaligned<T>(ptr);
            if (!BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }
        [MethodImpl(Inline)]
        public static T ReadBE<T>(byte* ptr) where T : unmanaged
        {
            var value = Unsafe.ReadUnaligned<T>(ptr);
            if (BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }

        [MethodImpl(Inline)]
        public static T Read<T>(ref byte ptr) where T : unmanaged
        {
            return Unsafe.ReadUnaligned<T>(ref ptr);
        }
        [MethodImpl(Inline)]
        public static T ReadLE<T>(ref byte ptr) where T : unmanaged
        {
            var value = Unsafe.ReadUnaligned<T>(ref ptr);
            if (!BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }
        [MethodImpl(Inline)]
        public static T ReadBE<T>(ref byte ptr) where T : unmanaged
        {
            var value = Unsafe.ReadUnaligned<T>(ref ptr);
            if (BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }

        /// <summary> Writes a <typeparamref name="T"/> element to the array, without bounds check, in the platform's native byte order. </summary>
        [MethodImpl(Inline)]
        public static void Write<T>(byte[] arr, int bytePos, T value) where T : unmanaged
        {
            Write<T>(ref Unsafe.Add(ref GetByteRef(arr), bytePos), value);
        }

        /// <summary> Writes a <typeparamref name="T"/> element to the array, without bounds check, in little-endian byte order. </summary>
        [MethodImpl(Inline)]
        public static void WriteLE<T>(byte[] arr, int bytePos, T value) where T : unmanaged
        {
            WriteLE<T>(ref Unsafe.Add(ref GetByteRef(arr), bytePos), value);
        }
        /// <summary> Writes a <typeparamref name="T"/> element to the array, without bounds check, in big-endian byte order. </summary>
        [MethodImpl(Inline)]
        public static void WriteBE<T>(byte[] arr, int bytePos, T value) where T : unmanaged
        {
            WriteBE<T>(ref Unsafe.Add(ref GetByteRef(arr), bytePos), value);
        }

        [MethodImpl(Inline)]
        public static void Write<T>(Span<byte> buf, int bytePos, T value) where T : unmanaged
        {
            Write<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos), value);
        }
        [MethodImpl(Inline)]
        public static void WriteLE<T>(Span<byte> buf, int bytePos, T value) where T : unmanaged
        {
            WriteLE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos), value);
        }
        [MethodImpl(Inline)]
        public static void WriteBE<T>(Span<byte> buf, int bytePos, T value) where T : unmanaged
        {
            WriteBE<T>(ref Unsafe.Add(ref GetByteRef(buf), bytePos), value);
        }

        [MethodImpl(Inline)]
        public static void Write<T>(byte* ptr, T value) where T : unmanaged
        {
            Unsafe.WriteUnaligned<T>(ptr, value);
        }
        [MethodImpl(Inline)]
        public static void WriteLE<T>(byte* ptr, T value) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Unsafe.WriteUnaligned<T>(ptr, value);
        }
        [MethodImpl(Inline)]
        public static void WriteBE<T>(byte* ptr, T value) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Unsafe.WriteUnaligned<T>(ptr, value);
        }

        [MethodImpl(Inline)]
        public static void Write<T>(ref byte ptr, T value) where T : unmanaged
        {
            Unsafe.WriteUnaligned<T>(ref ptr, value);
        }
        [MethodImpl(Inline)]
        public static void WriteLE<T>(ref byte ptr, T value) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Unsafe.WriteUnaligned<T>(ref ptr, value);
        }
        [MethodImpl(Inline)]
        public static void WriteBE<T>(ref byte ptr, T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Unsafe.WriteUnaligned<T>(ref ptr, value);
        }

        #endregion

        /// <summary> Reverse bytes of a primitive value. </summary>
        /// <typeparam name="T">The type of the value to reverse the bytes. Must be a blittable type with size <c>1, 2, 4, or 8</c>. </typeparam>
        [MethodImpl(Inline)]
        public static T BSwap<T>(T value) where T : unmanaged
        {
            static V I<V>(T v) => Unsafe.As<T, V>(ref v);
            static T O<V>(V v) => Unsafe.As<V, T>(ref v);

            if (sizeof(T) == 1) return O(BinaryPrimitives.ReverseEndianness(I<byte>(value)));
            if (sizeof(T) == 2) return O(BinaryPrimitives.ReverseEndianness(I<ushort>(value)));
            if (sizeof(T) == 4) return O(BinaryPrimitives.ReverseEndianness(I<uint>(value)));
            if (sizeof(T) == 8) return O(BinaryPrimitives.ReverseEndianness(I<ulong>(value)));

            throw new NotSupportedException();
        }

        /// <summary> Swaps the bytes of the elements in <paramref name="buf"/></summary>
        public static void BSwapBulk<T>(Span<T> buf) where T : unmanaged
        {
            BSwapBulk(ref GetByteRef(buf), buf.Length * sizeof(T), sizeof(T));
        }
        /// <summary> Reverses the bytes of each <paramref name="elemSize"/> chunk in <paramref name="ptr"/></summary>
        /// <param name="sizeInBytes">Size in bytes of <paramref name="ptr"/>. Must be a multiple of <paramref name="elemSize"/>.</param>
        /// <param name="elemSize">Chunk size in bytes. Must be in {1, 2, 4, 8}</param>
        public static void BSwapBulk(ref byte ptr, nint sizeInBytes, int elemSize)
        {
            var BSWAP2_SHUFFMASK = Vector256.Create(
                 1,  0, /**/  3,  2, /**/  5,  4, /**/  7,  6, 
                 9,  8, /**/ 11, 10, /**/ 14, 13, /**/ 16, 15, 
                18, 17, /**/ 20, 19, /**/ 22, 21, /**/ 24, 23,
                26, 25, /**/ 28, 27, /**/ 30, 29, /**/ 32, 31
            ).AsByte();
            var BSWAP4_SHUFFMASK = Vector256.Create(
                 3,  2,  1,  0, /**/  7,  6,  5,  4,
                11, 10,  9,  8, /**/ 15, 14, 13, 12,
                19, 18, 17, 16, /**/ 23, 22, 21, 20,
                27, 26, 25, 24, /**/ 31, 30, 29, 28
            ).AsByte();
            var BSWAP8_SHUFFMASK = Vector256.Create(
                 7,  6,  5,  4,  3,  2,  1,  0,
                15, 14, 13, 12, 11, 10,  9,  8,
                23, 22, 21, 20, 19, 18, 17, 16,
                31, 30, 29, 28, 27, 26, 25, 24
            ).AsByte();

            switch (elemSize) {
                case 1: break;
                case 2: BSwapBulk<ushort>(ref ptr, sizeInBytes, BSWAP2_SHUFFMASK); break;
                case 4: BSwapBulk<uint  >(ref ptr, sizeInBytes, BSWAP4_SHUFFMASK); break;
                case 8: BSwapBulk<ulong >(ref ptr, sizeInBytes, BSWAP8_SHUFFMASK); break;
                default: Ensure.That(false, "bswap element size must be in {1, 2, 4, 8}"); break;
            }
        }
        private static void BSwapBulk<T>(ref byte ptr, nint sizeInBytes, Vector256<byte> shuffMask) where T : unmanaged
        {
            ref byte startPtr = ref ptr;

            if (Avx2.IsSupported) {
                ref byte endPtrAlign = ref Unsafe.Add(ref startPtr, sizeInBytes & ~31);
                while (Unsafe.IsAddressLessThan(ref ptr, ref endPtrAlign)) {
                    ref var vec = ref Unsafe.As<byte, Vector256<byte>>(ref ptr);
                    vec = Avx2.Shuffle(vec, shuffMask);
                    ptr = ref Unsafe.Add(ref ptr, 32);
                }
            }
            if (Ssse3.IsSupported) {
                ref byte endPtrAlign = ref Unsafe.Add(ref startPtr, sizeInBytes & ~15);
                var mask128 = shuffMask.GetLower();
                while (Unsafe.IsAddressLessThan(ref ptr, ref endPtrAlign)) {
                    ref var vec = ref Unsafe.As<byte, Vector128<byte>>(ref ptr);
                    vec = Ssse3.Shuffle(vec, mask128);
                    ptr = ref Unsafe.Add(ref ptr, 16);
                }
            }
            ref byte endPtr = ref Unsafe.Add(ref startPtr, sizeInBytes);
            while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr)) {
                ref var val = ref Unsafe.As<byte, T>(ref ptr);
                val = Mem.BSwap(val);
                ptr = ref Unsafe.Add(ref ptr, sizeof(T));
            }
        }

        /// <summary> Returns whether all elements in the array are equal the specified value. </summary>
        public static bool AllEquals<T>(ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            if (Vector.IsHardwareAccelerated && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
                if (Unsafe.SizeOf<T>() == 1) {
                    return AllEqualsSimd(
                        ref Unsafe.As<T, byte>(ref GetRef(span)),
                        Unsafe.As<T, byte>(ref value),
                        span.Length
                    );
                }
                if (Unsafe.SizeOf<T>() == 2) {
                    return AllEqualsSimd(
                        ref Unsafe.As<T, short>(ref GetRef(span)),
                        Unsafe.As<T, short>(ref value),
                        span.Length
                    );
                }
                if (Unsafe.SizeOf<T>() == 4) {
                    return AllEqualsSimd(
                        ref Unsafe.As<T, int>(ref GetRef(span)),
                        Unsafe.As<T, int>(ref value),
                        span.Length
                    );
                }
                if (Unsafe.SizeOf<T>() == 8) {
                    return AllEqualsSimd(
                        ref Unsafe.As<T, long>(ref GetRef(span)),
                        Unsafe.As<T, long>(ref value),
                        span.Length
                    );
                }
            }
            ref T ptr = ref GetRef(span);
            ref T endPtr = ref Unsafe.Add(ref ptr, span.Length);

            while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr)) {
                if (!ptr.Equals(value)) {
                    return false;
                }
                ptr = ref Unsafe.Add(ref ptr, 1);
            }
            return true;
        }
        private static bool AllEqualsSimd<T>(ref T ptr, T val, nint count) where T : unmanaged
        {
            ref T endPtr = ref Unsafe.Add(ref ptr, count);
            int vecSize = Vector<T>.Count;

            if (Vector.IsHardwareAccelerated && vecSize >= sizeof(T)) {
                ref T endPtrAlign = ref Unsafe.Add(ref ptr, count / vecSize * vecSize);
                var vecVal = new Vector<T>(val);

                while (Unsafe.IsAddressLessThan(ref ptr, ref endPtrAlign)) {
                    var currVal = Unsafe.As<T, Vector<T>>(ref ptr);
                    if (!Vector.EqualsAll(vecVal, currVal)) {
                        return false;
                    }
                    ptr = ref Unsafe.Add(ref ptr, vecSize);
                }
            }
            while (Unsafe.IsAddressLessThan(ref ptr, ref endPtr)) {
                if (!ptr.Equals(val)) {
                    return false;
                }
                ptr = ref Unsafe.Add(ref ptr, 1);
            }
            return true;
        }

        /// <summary> Allocates a unmanaged memory block of <paramref name="count"/> <typeparamref name="T"/> elements from the heap. </summary>
        /// <remarks>The returned pointer should be released with <see cref="Free{T}(T*)"/> when it is no longer needed.</remarks>
        public static T* Alloc<T>(nint count) where T : unmanaged
        {
            nuint numBytes = checked((nuint)(count * sizeof(T)));
            return (T*)NativeMemory.Alloc(numBytes);
        }
        /// <summary> Frees a memory block allocated by <see cref="Alloc{T}(nint)"/>. </summary>
        /// <remarks>This method does nothing if <paramref name="ptr"/> is null.</remarks>
        public static void Free(void* ptr)
        {
            NativeMemory.Free(ptr);
        }

        /// <summary> Allocates a unmanaged memory block of <paramref name="count"/> <typeparamref name="T"/> elements from the heap, then fills it with the default <typeparamref name="T"/> value. </summary>
        /// <remarks>The returned pointer should be released with <see cref="Free{T}(T*)"/> when it is no longer needed.</remarks>
        public static T* AllocZeroed<T>(nint count) where T : unmanaged
        {
            T* ptr = Alloc<T>(count);
            Clear(ptr, count);
            return ptr;
        }

        /// <summary> Allocates a unmanaged memory block for one <typeparamref name="T"/> element from the heap, then set it with the default <typeparamref name="T"/> value. </summary>
        /// <remarks>The returned pointer should be released with <see cref="Free{T}(T*)"/> when it is no longer needed.</remarks>
        public static T* New<T>() where T : unmanaged
        {
            T* ptr = Alloc<T>(1);
            *ptr = default;
            return ptr;
        }

        public static void Fill<T>(T* ptr, nint count, T value) where T : unmanaged
        {
            while (count > 0) {
                var s = new Span<T>(ptr, (int)Math.Min(count, int.MaxValue));
                s.Fill(value);
                ptr += s.Length;
                count -= s.Length;
            }
        }
        public static void Clear<T>(T* ptr, nint count) where T : unmanaged
        {
            while (count > 0) {
                var s = new Span<T>(ptr, (int)Math.Min(count, int.MaxValue));
                s.Clear();
                ptr += s.Length;
                count -= s.Length;
            }
        }
    }
}
