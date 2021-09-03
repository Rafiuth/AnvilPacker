using System.Collections;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Nbt
{
    public unsafe struct NbtArrayView<T> : IEnumerable<T> where T : unmanaged
    {
        public byte[] Data { get; }
        public int ByteOffset { get; }
        public int Count { get; }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                return Mem.CreateSpan<byte, T>(ref Data[ByteOffset], Count * sizeof(T));
            }
        }
        public ref T this[int index] => ref Span[index];

        public NbtArrayView(byte[] data, int offset, int count)
        {
            _ = data.AsSpan(offset, count * sizeof(T)); //bounds check
            Data = data;
            ByteOffset = offset;
            Count = count;
        }

        public T[] ToArray() => Span.ToArray();

        public Enumerator GetEnumerator() => new Enumerator(Data, ByteOffset, Count);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private readonly byte[] _array;
            private int _pos, _end;

            internal Enumerator(byte[] array, int offset, int count)
            {
                _array = array;
                _pos = offset - sizeof(T);
                _end = offset + count * sizeof(T);
            }

            public T Current => Mem.Read<T>(_array, _pos);

            public bool MoveNext()
            {
                if (_pos < _end) {
                    _pos += sizeof(T);
                    return true;
                }
                return false;
            }

            object IEnumerator.Current => Current;
            public void Dispose() { }
            public void Reset() => throw new NotSupportedException();
        }
    }
}