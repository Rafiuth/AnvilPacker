using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnvilPacker.Data
{
    /// <summary> A read-only stream that limits the amount of data that can be read. </summary>
    public class ClampedStream : Stream
    {
        public Stream BaseStream { get; }
        public int Remaining { get; private set; }
        private bool _leaveOpen;

        public ClampedStream(Stream s, int length, bool leaveOpen = false)
        {
            BaseStream = s;
            Remaining = length;
            Length = length;
            _leaveOpen = leaveOpen;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position {
            get => Length - Remaining;
            set => throw new NotSupportedException(); 
        }
        public override int ReadByte()
        {
            if (Remaining <= 0) return -1;
            Remaining--;
            return BaseStream.ReadByte();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Remaining <= 0) return 0;
            int c = BaseStream.Read(buffer, offset, Math.Min(Remaining, count));
            Remaining -= c;
            return c;
        }
        public override int Read(Span<byte> buffer)
        {
            if (Remaining <= 0) return 0;
            int c = BaseStream.Read(buffer.Slice(0, Math.Min(Remaining, buffer.Length)));
            Remaining -= c;
            return c;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Remaining <= 0) return 0;
            int c = await BaseStream.ReadAsync(buffer.Slice(0, Math.Min(Remaining, buffer.Length)), cancellationToken).ConfigureAwait(false);
            Remaining -= c;
            return c;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Remaining <= 0) return 0;
            int c = await BaseStream.ReadAsync(buffer, offset, Math.Min(Remaining, count), cancellationToken).ConfigureAwait(false);
            Remaining -= c;
            return c;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !_leaveOpen) {
                BaseStream.Dispose();
            }
        }
    }
}
