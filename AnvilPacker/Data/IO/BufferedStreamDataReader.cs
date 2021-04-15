using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Implementation of <see cref="DataReader"/> backed by a <see cref="Stream"/> </summary>
    public class BufferedStreamDataReader : DataReader
    {
        private const int MIN_BUF_SIZE = 256;

        private readonly Stream _s;
        private readonly bool _leaveOpen;
        private byte[] _buf;
        private int _pos, _len;

        /// <remarks> Note that since this reader is buffered, the base stream position may not be accurate. </remarks>
        public Stream BaseStream => _s;

        public override long Position
        {
            get => _s.Position;
            set {
                _pos = _len; //discard buffer; could be optimized for small seeks
                _s.Position = value;
            }
        }

        public BufferedStreamDataReader(Stream s, bool leaveOpen = false, int bufferSize = 4096)
        {
            if (bufferSize < MIN_BUF_SIZE) {
                throw new ArgumentException($"Must be at least {MIN_BUF_SIZE}.", nameof(bufferSize));
            }
            _s = s;
            _leaveOpen = leaveOpen;
            _buf = new byte[bufferSize];
        }

        public unsafe override T Read<T>()
        {
            Debug.Assert(sizeof(T) <= MIN_BUF_SIZE);

            if (_len - _pos < sizeof(T)) {
                FillBuffer(sizeof(T));
            }
            T value = Mem.ReadBE<T>(_buf, _pos);
            _pos += sizeof(T);
            return value;
        }
        public override void ReadBytes(Span<byte> dest)
        {
            //try read data from buffer, if possible
            int bufRem = Math.Min(_len - _pos, dest.Length);
            if (bufRem > 0) {
                _buf.AsSpan(_pos, bufRem).CopyTo(dest);
                dest = dest[bufRem..];

                _pos += bufRem;
            }

            //read data from the underlying stream
            while (dest.Length > 0) {
                int count = _s.Read(dest);
                if (count <= 0) {
                    throw new EndOfStreamException();
                }
                dest = dest[count..];
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void FillBuffer(int minCount)
        {
            int rem = _len - _pos;

            if (rem > 0) {
                //shift the buffered data to the beginning
                _buf.AsSpan(_pos, rem).CopyTo(_buf.AsSpan());
            }
            _pos = 0;
            _len = rem;

            while (_len < minCount) {
                int read = _s.Read(_buf, _len, _buf.Length - _len);

                if (read <= 0) {
                    throw new EndOfStreamException();
                }
                _len += read;
            }
        }

        public override void Dispose()
        {
            if (!_leaveOpen) {
                _s.Dispose();
            }
        }
    }
}
