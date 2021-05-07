using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public class Compressors
    {
        /// <param name="quality">A number representing quality of the Brotli compression. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A number representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        public static DataWriter NewBrotliEncoder(DataWriter output, bool leaveOpen = false, int quality = 10, int windowBits = 22)
        {
            return new DataWriter(new BrotliStream(output.BaseStream, CompressionLevel.Optimal, leaveOpen));
        }
        public static DataReader NewBrotliDecoder(Stream input, bool leaveOpen = false)
        {
            return new DataReader(new BrotliStream(input, CompressionMode.Decompress, leaveOpen));
        }

        private class BrotliEncStream : Stream
        {
            private BrotliEncoder _enc;
            private byte[] _buf = new byte[4096];
            private Stream _stream;
            private bool _leaveOpen;

            public BrotliEncStream(Stream output, bool leaveOpen, int quality, int windowBits)
            {
                _stream = output;
                _leaveOpen = leaveOpen;
                _enc = new BrotliEncoder(quality, windowBits);
            }
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                DoWrite(buffer, false);
            }

            private void DoWrite(ReadOnlySpan<byte> data, bool isFinalBlock)
            {
                while (true) {
                    var result = _enc.Compress(
                        data, _buf, 
                        out int bytesConsumed, 
                        out int bytesWritten, 
                        isFinalBlock
                    );
                    _stream.Write(_buf, 0, bytesWritten);
                    data = data[bytesConsumed..];

                    if (result is OperationStatus.Done or OperationStatus.NeedMoreData) break;
                    Ensure.That(result is OperationStatus.DestinationTooSmall);
                }
            }
            public override void Flush()
            {
                while (true) {
                    var result = _enc.Flush(_buf, out int bytesWritten);
                    _stream.Write(_buf, 0, bytesWritten);

                    if (result is OperationStatus.Done or OperationStatus.NeedMoreData) break;
                    Ensure.That(result is OperationStatus.DestinationTooSmall);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) {
                    DoWrite(default, true);
                }
                if (disposing && !_leaveOpen) {
                    _stream.Dispose();
                }
                _enc.Dispose();
                base.Dispose(disposing);
            }
            #region boring stuff
            public override bool CanRead => false;
            public override bool CanWrite => true;
            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(buffer.AsSpan(offset, count));
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            #endregion
        }
    }
}