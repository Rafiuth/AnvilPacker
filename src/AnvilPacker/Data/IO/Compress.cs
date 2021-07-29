using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using AnvilPacker.Util;
using LibDeflate;
using LdCompressor = LibDeflate.Compressor;
using LdDecompressor = LibDeflate.Decompressor;

namespace AnvilPacker.Data
{
    public class Compressors
    {
        /// <param name="quality">A number representing quality of the Brotli compression. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A number representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        public static DataWriter NewBrotliEncoder(DataWriter output, bool leaveOpen = false, int quality = 10, int windowBits = 22)
        {
            return new DataWriter(new BrotliEncStream(output.BaseStream, leaveOpen, quality, windowBits));
        }
        public static DataReader NewBrotliDecoder(Stream input, bool leaveOpen = false)
        {
            return new DataReader(new BrotliStream(input, CompressionMode.Decompress, leaveOpen));
        }

        //This exists because BCL's BrotliStream configurability is awful
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

    public class DeflateHelper : IDisposable
    {
        private byte[] _inBuf, _outBuf;
        private readonly int _maxInSize, _maxOutSize;
        private readonly int _compressionLevel;

        //FIXME: could use a single instance, LibDeflate.NET unfortunately abstracts that away
        private LdCompressor[] _encs = new LdCompressor[3];
        private LdDecompressor[] _decs = new LdDecompressor[3];

        public byte[] InputBuffer => _inBuf;
        public byte[] OutputBuffer => _outBuf;

        public DeflateHelper(int maxInSize, int maxOutSize, int compressionLevel = 5, int initialInSize = 1024 * 16, int initialOutSize = 1024 * 64)
        {
            _inBuf = new byte[initialInSize];
            _outBuf = new byte[initialOutSize];
            _compressionLevel = compressionLevel;
            _maxInSize = maxInSize;
            _maxOutSize = maxOutSize;
        }

        public static bool HasGzipHeader(ReadOnlySpan<byte> data)
        {
            return data.Length >= 10 &&
                   data[0] == 0x1F &&
                   data[1] == 0x8B &&
                   data[2] == 0x08;
        }

        /// <summary> Allocates a buffer of at least <paramref name="count"/> bytes, that can be used as the input for other methods of this class. </summary>
        public ArraySegment<byte> AllocInBuffer(int count)
        {
            EnsureCapacity(ref _inBuf, count, _maxInSize);
            return new ArraySegment<byte>(_inBuf, 0, count);
        }

        public ArraySegment<byte> Compress(ReadOnlySpan<byte> input, DeflateFlavor flavor)
        {
            var compressor = GetCompressor(flavor);
            while (true) {
                var len = compressor.Compress(input, _outBuf);

                if (len != 0) {
                    return new ArraySegment<byte>(_outBuf, 0, len);
                }
                EnsureCapacity(ref _outBuf, _outBuf.Length * 2, _maxOutSize);
            }
        }

        public ArraySegment<byte> Decompress(ReadOnlySpan<byte> input, DeflateFlavor flavor)
        {
            var decompressor = GetDecompressor(flavor);
            while (true) {
                var status = decompressor.Decompress(input, _outBuf, out int bytesWritten);

                switch (status) {
                    case OperationStatus.Done: {
                        return new ArraySegment<byte>(_outBuf, 0, bytesWritten);
                    }
                    case OperationStatus.DestinationTooSmall: {
                        EnsureCapacity(ref _outBuf, _outBuf.Length * 2, _maxOutSize);
                        break;
                    }
                    default: {
                        throw new InvalidDataException();
                    }
                }
            }
        }

        private LdCompressor GetCompressor(DeflateFlavor flavor)
        {
            return _encs[(int)flavor] ??= CreateCompressor(flavor);
        }
        private LdCompressor CreateCompressor(DeflateFlavor flavor)
        {
            return flavor switch {
                DeflateFlavor.Zlib  => new ZlibCompressor(_compressionLevel),
                DeflateFlavor.Gzip  => new GzipCompressor(_compressionLevel),
                DeflateFlavor.Plain => new DeflateCompressor(_compressionLevel),
                _ => throw new NotSupportedException()
            };
        }
        private LdDecompressor GetDecompressor(DeflateFlavor flavor)
        {
            return _decs[(int)flavor] ??= CreateDecompressor(flavor);
        }
        private LdDecompressor CreateDecompressor(DeflateFlavor flavor)
        {
            return flavor switch {
                DeflateFlavor.Zlib  => new ZlibDecompressor(),
                DeflateFlavor.Gzip  => new GzipDecompressor(),
                DeflateFlavor.Plain => new DeflateDecompressor(),
                _ => throw new NotSupportedException()
            };
        }

        private void EnsureCapacity(ref byte[] buf, int minLen, int limit)
        {
            if (buf.Length >= minLen) return;

            int newLen = Math.Max(minLen + 1024, buf.Length * 2);
            if (newLen > limit && minLen <= limit) {
                newLen = limit;
            }
            if (newLen > limit) {
                throw new NotSupportedException("Tried to expand inflater/deflater buffer beyond defined limit.");
            }
            Array.Resize(ref buf, newLen);
        }

        public void Dispose()
        {
            foreach (var enc in _encs) enc?.Dispose();
            foreach (var dec in _decs) dec?.Dispose();
        }
    }
    public enum DeflateFlavor
    {
        Zlib,
        Gzip,
        Plain,
    }
}