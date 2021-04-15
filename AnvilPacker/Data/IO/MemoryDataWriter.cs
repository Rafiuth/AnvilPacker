using System;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public class MemoryDataWriter : DataWriter
    {
        private byte[] _buf;
        private int _pos;

        public byte[] Buffer => _buf;
        public override long Position
        {
            get => _pos;
            set {
                EnsureCapacity(checked((int)(value - _pos)));
                _pos = (int)value;
            }
        }
        public Span<byte> BufferSpan => Buffer.AsSpan(0, _pos);

        public MemoryDataWriter(int initialCapacity = 4096)
        {
            _buf = new byte[initialCapacity];
            _pos = 0;
        }

        public unsafe override void Write<T>(T value)
        {
            EnsureCapacity(sizeof(T));
            Mem.WriteBE(_buf, _pos, value);
            _pos += sizeof(T);
        }
        public override void WriteBytes(ReadOnlySpan<byte> buf)
        {
            EnsureCapacity(buf.Length);
            buf.CopyTo(_buf.AsSpan(_pos));
            _pos += buf.Length;
        }
        private void EnsureCapacity(int length)
        {
            if (_pos + length > _buf.Length) {
                Array.Resize(ref _buf, _buf.Length * 2);
            }
        }

        public override void Dispose()
        {
        }
    }
}
