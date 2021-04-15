using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    /// <summary> Provides high performance, low level memory related functions. </summary>
    /// <remarks>
    /// Most methods in this class are unsafe, in the sense that they do not
    /// validate parameters, nor perform bounds check. It is the user's responsability
    /// to ensure that arguments are valid and that no invalid memory accesses are attempted.
    /// </remarks>
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
        public static ref byte GetByteRef<T>(T[] arr, nint byteOffset = 0)
        {
            // return (byte*)&buf[0] + byteOffset
            ref byte ptr = ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(arr));
            return ref Unsafe.AddByteOffset(ref ptr, byteOffset);
        }

        /// <summary> Returns a reference to the n-th element of the array. </summary>
        [MethodImpl(Inline)]
        public static ref T GetRef<T>(T[] arr, nint elemOffset = 0)
        {
            // return &buf[bytePos]
            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), elemOffset);
        }

        /// <summary> Returns a reference to the n-th element of the span. </summary>
        [MethodImpl(Inline)]
        public static ref T GetRef<T>(ReadOnlySpan<T> span, nint elemOffset = 0)
        {
            // return &buf[bytePos]
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(span), elemOffset);
        }

        /// <summary> Reads a <typeparamref name="T"/> element from the array, in the platform's native byte order. Unaligned access is assumed. </summary>
        [MethodImpl(Inline)]
        public static T Read<T>(byte[] buf, nint bytePos) where T : unmanaged
        {
            return Read<T>(ref GetByteRef(buf, 0), bytePos);
        }

        /// <summary> Reads a <typeparamref name="T"/> element from the array, in little-endian byte order. Unaligned access is assumed. </summary>
        /// <remarks>
        /// <typeparamref name="T"/> must be a primitive type.
        /// This method does not perform bounds check. Use carefully. 
        /// </remarks>
        [MethodImpl(Inline)]
        public static T ReadLE<T>(byte[] buf, nint bytePos) where T : unmanaged
        {
            return ReadLE<T>(ref GetByteRef(buf, 0), bytePos);
        }
        /// <summary> Reads a <typeparamref name="T"/> element from the array, in big-endian byte order. Unaligned access is assumed. </summary>
        /// <remarks>
        /// <typeparamref name="T"/> must be a primitive type.
        /// This method does not perform bounds check. Use carefully. 
        /// </remarks>
        [MethodImpl(Inline)]
        public static T ReadBE<T>(byte[] buf, nint bytePos) where T : unmanaged
        {
            return ReadBE<T>(ref GetByteRef(buf, 0), bytePos);
        }

        [MethodImpl(Inline)]
        public static T Read<T>(byte* ptr, nint bytePos) where T : unmanaged
        {
            return Unsafe.ReadUnaligned<T>(&ptr[bytePos]);
        }
        [MethodImpl(Inline)]
        public static T ReadLE<T>(byte* ptr, nint bytePos) where T : unmanaged
        {
            return ReadLE<T>(ref Unsafe.AsRef<byte>(ptr), bytePos);
        }
        [MethodImpl(Inline)]
        public static T ReadBE<T>(byte* ptr, nint bytePos) where T : unmanaged
        {
            return ReadBE<T>(ref Unsafe.AsRef<byte>(ptr), bytePos);
        }

        [MethodImpl(Inline)]
        public static T Read<T>(ref byte ptr, nint bytePos) where T : unmanaged
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.AddByteOffset(ref ptr, bytePos));
        }
        [MethodImpl(Inline)]
        public static T ReadLE<T>(ref byte ptr, nint bytePos) where T : unmanaged
        {
            var value = Read<T>(ref ptr, bytePos);
            if (!BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }
        [MethodImpl(Inline)]
        public static T ReadBE<T>(ref byte ptr, nint bytePos) where T : unmanaged
        {
            var value = Read<T>(ref ptr, bytePos);
            if (BitConverter.IsLittleEndian) {
                return BSwap(value);
            }
            return value;
        }

        /// <summary> Writes a <typeparamref name="T"/> element to the array, in the platform's native byte order. Unaligned access is assumed. </summary>
        [MethodImpl(Inline)]
        public static void Write<T>(byte[] buf, nint bytePos, T value)
        {
            Unsafe.WriteUnaligned<T>(ref GetByteRef(buf, bytePos), value);
        }

        /// <summary> Writes a <typeparamref name="T"/> element to the array, in little-endian byte order. Unaligned access is assumed. </summary>
        /// <remarks>
        /// <typeparamref name="T"/> must be a primitive type.
        /// This method does not perform bounds check. Use carefully. 
        /// </remarks>
        [MethodImpl(Inline)]
        public static void WriteLE<T>(byte[] buf, nint bytePos, T value) where T : unmanaged
        {
            WriteLE<T>(ref GetByteRef(buf, 0), bytePos, value);
        }
        /// <summary> Writes a <typeparamref name="T"/> element to the array, in big-endian byte order. Unaligned access is assumed. </summary>
        /// <remarks>
        /// <typeparamref name="T"/> must be a primitive type.
        /// This method does not perform bounds check. Use carefully. 
        /// </remarks>
        [MethodImpl(Inline)]
        public static void WriteBE<T>(byte[] buf, nint bytePos, T value) where T : unmanaged
        {
            WriteBE<T>(ref GetByteRef(buf, 0), bytePos, value);
        }

        [MethodImpl(Inline)]
        public static void Write<T>(byte* ptr, nint bytePos, T value) where T : unmanaged
        {
            Unsafe.WriteUnaligned<T>(ptr + bytePos, value);
        }
        [MethodImpl(Inline)]
        public static void WriteLE<T>(byte* ptr, nint bytePos, T value) where T : unmanaged
        {
            WriteLE<T>(ref Unsafe.AsRef<byte>(ptr), bytePos, value);
        }
        [MethodImpl(Inline)]
        public static void WriteBE<T>(byte* ptr, nint bytePos, T value) where T : unmanaged
        {
            WriteBE<T>(ref Unsafe.AsRef<byte>(ptr), bytePos, value);
        }


        [MethodImpl(Inline)]
        public static void Write<T>(ref byte ptr, nint bytePos, T value) where T : unmanaged
        {
            Unsafe.WriteUnaligned<T>(ref Unsafe.AddByteOffset(ref ptr, bytePos), value);
        }
        [MethodImpl(Inline)]
        public static void WriteLE<T>(ref byte ptr, nint bytePos, T value) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Write<T>(ref ptr, bytePos, value);
        }
        [MethodImpl(Inline)]
        public static void WriteBE<T>(ref byte ptr, nint bytePos, T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) {
                value = BSwap(value);
            }
            Write<T>(ref ptr, bytePos, value);
        }

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

        /// <summary> Allocates a unmanaged memory block of <paramref name="count"/> <typeparamref name="T"/> elements from the heap. </summary>
        /// <remarks>The returned pointer should be released with <see cref="Free{T}(T*)"/> when it is no longer needed.</remarks>
        public static T* Alloc<T>(nint count) where T : unmanaged
        {
            return (T*)Marshal.AllocHGlobal(sizeof(T) * count);
        }
        /// <summary> Frees a memory block allocated by <see cref="Alloc{T}(nint)"/>. </summary>
        /// <remarks>This method does nothing if <paramref name="ptr"/> is null.</remarks>
        public static void Free<T>(T* ptr) where T : unmanaged
        {
            Marshal.FreeHGlobal((IntPtr)ptr);
        }

        /// <summary> Allocates a unmanaged memory block of <paramref name="count"/> <typeparamref name="T"/> elements from the heap, then fills it with the default <typeparamref name="T"/> value. </summary>
        /// <remarks>The returned pointer should be released with <see cref="Free{T}(T*)"/> when it is no longer needed.</remarks>
        public static T* ZeroAlloc<T>(nint count) where T : unmanaged
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
