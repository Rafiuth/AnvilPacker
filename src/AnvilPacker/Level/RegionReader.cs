using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;
using LibDeflate;

namespace AnvilPacker.Level
{
    //TODO: support for external chunks aka '.mcc' files
    //https://minecraft.gamepedia.com/Region_file_format
    public class RegionReader : IDisposable
    {
        private const int MAX_COMPRESSED_CHUNK_SIZE = 256 * 4096; //1MB / .mca hard limit
        private const int MAX_UNCOMPRESSED_CHUNK_SIZE = 1024 * 1024 * 16;

        private readonly DataReader _s;
        private readonly int[] _locations = new int[1024];

        private Decompressor[] _decompressors = new Decompressor[2];
        private byte[] _inBuf = new byte[1024 * 32];
        private byte[] _outBuf = new byte[1024 * 256];

        public RegionReader(string filename)
        {
            _s = new DataReader(File.OpenRead(filename), false);
            if (_s.Length > 8192) {
                _s.ReadBulkBE<int>(_locations);
            }
        }

        public IEnumerable<(CompoundTag Tag, int X, int Z)> ReadAll()
        {
            //Read chunks sequentially to improve perf on slow medias (HDDs/whatever)
            //Don't really know how effective this is, hard to test because of caching.
            foreach (var (loc, i) in _locations.Select((v, i) => (v, i)).OrderBy(e => e.v)) {
                var tag = Read(loc);
                if (tag != null) {
                    yield return (tag, i & 31, i >> 5);
                }
            }
        }
        public CompoundTag Read(int x, int z)
        {
            return Read(_locations[GetIndex(x, z)]);
        }

        private CompoundTag Read(int loc)
        {
            int offset = (loc >> 8) * 4096;
            int length = (loc & 0xFF) * 4096;
            if (length == 0) {
                return null;
            }
            _s.Position = offset;
            int actualLen = _s.ReadIntBE() - 1;
            byte compressionType = _s.ReadByte();

            if (actualLen > length) {
                throw new InvalidDataException($"Corrupted chunk: declared length larger than sector count.");
            }
            var rawStream = _s.AsStream(actualLen);

            if (compressionType == 3) {
                return NbtIO.Read(rawStream);
            }
            var decompressor = compressionType switch {
                1 => _decompressors[0] ??= new GzipDecompressor(),
                2 => _decompressors[1] ??= new ZlibDecompressor(),
                _ => throw new NotSupportedException($"Chunk compression method {compressionType}")
            };
            EnsureCapacity(ref _inBuf, actualLen, MAX_COMPRESSED_CHUNK_SIZE);
            var inBuf = _inBuf.AsSpan(0, actualLen);
            _s.ReadBytes(inBuf);

            while (true) {
                switch (decompressor.Decompress(inBuf, _outBuf, out int bytesWritten, out _)) {
                    case OperationStatus.Done: {
                        var mem = new MemoryStream(_outBuf, 0, bytesWritten);
                        return NbtIO.Read(mem);
                    }
                    case OperationStatus.DestinationTooSmall: {
                        EnsureCapacity(ref _outBuf, _outBuf.Length + 1, MAX_UNCOMPRESSED_CHUNK_SIZE);
                        break; //try again
                    }
                    default: {
                        throw new InvalidDataException("Failed to decompress chunk");
                    }
                }
            }
        }

        private void EnsureCapacity(ref byte[] buf, int minLen, int limit)
        {
            if (buf.Length < minLen) {
                int newLen = Math.Max(minLen + 1024, buf.Length * 2);
                if (newLen > limit && minLen <= limit) {
                    newLen = limit;
                }
                if (newLen > limit) {
                    throw new NotSupportedException("Chunk data larger than allowed (trying to expand buffer too much)");
                }
                Array.Resize(ref buf, newLen);
            }
        }

        private static int GetIndex(int x, int z)
        {
            Ensure.InRange(x, z, 0, 31, "Invalid region chunk coords");
            return x + z * 32;
        }

        public void Dispose()
        {
            _s.Dispose();
            foreach (var decompressor in _decompressors) {
                decompressor?.Dispose();
            }
        }
    }
}
