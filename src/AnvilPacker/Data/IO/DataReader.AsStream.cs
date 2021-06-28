using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{    
    public partial class DataReader
    {
        /// <summary> Wraps this reader into a new <see cref="Stream"/>. </summary>
        /// <param name="length">Maximum number of bytes that can be read from the returned stream. Any remaining bytes will be consumed when the stream is disposed. </param>
        /// <param name="leaveOpen">Whether to close this reader when the returned stream is closed/disposed.</param>
        public Stream AsStream(long length = long.MaxValue, bool leaveOpen = true)
        {
            return new StreamWrapper(this, length, leaveOpen);
        }

        private class StreamWrapper : Stream
        {
            private DataReader _dr;
            private bool _leaveOpen;
            private long _remainingBytes;
            private long _length;

            private byte[] _buf => _dr._buf;
            private int _bufPos { get => _dr._bufPos; set => _dr._bufPos = value; }
            private int _bufLen => _dr._bufLen;

            public override bool CanRead => true;
            public override bool CanSeek => _dr.BaseStream.CanSeek;
            public override bool CanWrite => false;

            public override long Length => Math.Min(_length + _dr.Position, _dr.Length);
            public override long Position
            {
                get => _dr.Position;
                set => _dr.Position = value;
            }

            public StreamWrapper(DataReader dr, long length, bool leaveOpen)
            {
                _dr = dr;
                _remainingBytes = length;
                _length = length;
                _leaveOpen = leaveOpen;
            }

            public override int Read(Span<byte> dest)
            {
                if (dest.Length > _remainingBytes) {
                    dest = dest[0..(int)_remainingBytes];
                }
                int orgLen = dest.Length;
                //Duplicating logic here because DataReader will always throw on EOF
                int bufAvail = Math.Min(dest.Length, _bufLen - _bufPos);
                if (bufAvail > 0) {
                    _buf.AsSpan(_bufPos, bufAvail).CopyTo(dest);
                    _bufPos += bufAvail;
                    dest = dest[bufAvail..];
                }
                while (dest.Length > 0) {
                    int bytesRead = _dr.BaseStream.Read(dest);
                    if (bytesRead <= 0) break;
                    dest = dest[bytesRead..];
                }
                int totalBytesRead = orgLen - dest.Length;
                _remainingBytes -= totalBytesRead;
                return totalBytesRead;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer.AsSpan(offset, count));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin) {
                    case SeekOrigin.Begin:      break;
                    case SeekOrigin.Current:    offset += Position; break;
                    case SeekOrigin.End:        offset = Length - offset; break;
                    default: throw new ArgumentException("Invalid seek origin");
                }
                _dr.Position = offset;
                return offset;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _remainingBytes > 0) {
                    _dr.SkipBytes(checked((int)_remainingBytes));
                }
                if (disposing && !_leaveOpen) {
                    _dr.Dispose();
                }
                base.Dispose(disposing);
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
            public override void Flush()
            {
            }
        }
    }
}