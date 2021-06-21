using System;
using System.IO;

namespace AnvilPacker.Data
{

    /// <summary> Provides a wrapper around a stream that locks on a object for every operation. </summary>
    public class SynchedStream : Stream
    {
        public Stream BaseStream { get; }
        public object SyncRoot { get; }
        private bool _leaveOpen;

        public override bool CanRead => BaseStream.CanRead;
        public override bool CanSeek => BaseStream.CanSeek;
        public override bool CanWrite => BaseStream.CanWrite;

        public override long Length {
            get {
                lock (SyncRoot) {
                    return BaseStream.Length;
                }
            }
        }
        public override long Position
        {
            get {
                lock (SyncRoot) {
                    return BaseStream.Position;
                }
            }
            set {
                lock (SyncRoot) {
                    BaseStream.Position = value;
                }
            }
        }

        public SynchedStream(Stream baseStream, object syncRoot, bool leaveOpen = false)
        {
            BaseStream = baseStream;
            SyncRoot = syncRoot;
            _leaveOpen = leaveOpen;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (SyncRoot) {
                return BaseStream.Read(buffer, offset, count);
            }
        }
        public override int Read(Span<byte> buffer)
        {
            lock (SyncRoot) {
                return BaseStream.Read(buffer);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (SyncRoot) {
                BaseStream.Write(buffer, offset, count);
            }
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            lock (SyncRoot) {
                BaseStream.Write(buffer);
            }
        }
        public override void Flush()
        {
            lock (SyncRoot) {
                BaseStream.Flush();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (SyncRoot) {
                return BaseStream.Seek(offset, origin);
            }
        }
        public override void SetLength(long value)
        {
            lock (SyncRoot) {
                BaseStream.SetLength(value);
            }
        }

        public override void Close()
        {
            if (!_leaveOpen) {
                BaseStream.Close();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen) {
                BaseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}