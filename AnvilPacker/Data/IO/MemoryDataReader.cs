using System;
using System.IO;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Implementation of <see cref="DataReader"/> backed by a byte[] </summary>
    public class MemoryDataReader : DataReader
    {
        public byte[] Data { get; }
        private int _pos;

        public override long Position
        {
            get => _pos;
            set => _pos = checked((int)value);
        }

        /// <param name="data">The backing data array. Changes made to it will be reflected on the <see cref="MemoryDataReader"/> instance.</param>
        public MemoryDataReader(byte[] data)
        {
            Data = data;
        }

        public override void ReadBytes(Span<byte> dest)
        {
            Data.AsSpan(_pos, dest.Length).CopyTo(dest);
            _pos += dest.Length;
        }
        public unsafe override T Read<T>()
        {
            if (_pos + sizeof(T) > Data.Length) {
                throw new EndOfStreamException();
            }
            T value = Mem.ReadBE<T>(Data, _pos);
            _pos += sizeof(T);
            return value;
        }

        public override void Dispose()
        {
        }
    }
}
