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
        public Stream AsStream(bool leaveOpen = true)
        {
            return new StreamProxy(this, leaveOpen);
        }

        private class StreamProxy : Stream
        {
            private DataReader _dr;
            private bool _leaveOpen;

            private byte[] _buf => _dr._buf;
            private int _bufPos { get => _dr._bufPos; set => _dr._bufPos = value; }
            private int _bufLen => _dr._bufLen;


            public override bool CanRead => true;
            public override bool CanSeek => _dr.BaseStream.CanSeek;
            public override bool CanWrite => false;

            public override long Length => _dr.Length;
            public override long Position
            {
                get => _dr.Position;
                set => _dr.Position = value;
            }

            public StreamProxy(DataReader dr, bool leaveOpen)
            {
                _dr = dr;
                _leaveOpen = leaveOpen;
            }

            public override int Read(Span<byte> dest)
            {
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
                return orgLen - dest.Length;
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