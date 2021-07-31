using System;
using System.IO;

namespace AnvilPacker.Data
{
    /// <summary> Provides a wrapper around a stream that invokes a callback when it is disposed. </summary>
    public class TrackedStream : Stream
    {
        public Stream BaseStream { get; }

        private readonly Action _onDispose;
        private readonly bool _onDisposeCallBefore;
        private readonly bool _leaveOpen;

        private bool _disposed = false;

        public override bool CanRead => BaseStream.CanRead;
        public override bool CanSeek => BaseStream.CanSeek;
        public override bool CanWrite => BaseStream.CanWrite;

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        /// <param name="onDispose">Delegate to call when this stream is disposed.</param>
        /// <param name="callBefore">When true, the onDispose delegate will be called before the base stream is disposed; otherwise, the stream will be disposed first. </param>
        /// <param name="leaveOpen">Whether or not to leave the base stream open. The onDispose delegate will be called independently of this value. </param>
        public TrackedStream(Stream baseStream, Action onDispose, bool callBefore, bool leaveOpen = false)
        {
            BaseStream = baseStream;
            _leaveOpen = leaveOpen;
            _onDispose = onDispose;
            _onDisposeCallBefore = callBefore;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
        }
        public override int Read(Span<byte> buffer)
        {
            return BaseStream.Read(buffer);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStream.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            BaseStream.Write(buffer);
        }
        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            BaseStream.SetLength(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed) {
                _disposed = true;
                
                if (_onDisposeCallBefore) _onDispose.Invoke();
                
                if (!_leaveOpen) {
                    BaseStream.Dispose();
                }

                if (!_onDisposeCallBefore) _onDispose.Invoke();
            }
        }
    }
}