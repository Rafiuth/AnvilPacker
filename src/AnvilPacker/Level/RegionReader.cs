using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        private DeflateHelper _deflater = new DeflateHelper(MAX_COMPRESSED_CHUNK_SIZE, MAX_UNCOMPRESSED_CHUNK_SIZE);

        public int X { get; }
        public int Z { get; }

        /// <summary> Creates the region reader based on the specified file. </summary>
        public RegionReader(string path)
            : this(File.OpenRead(path), path)
        {
        }
        /// <summary> Creates the region reader based on the specified stream. </summary>
        /// <param name="path">The path of the region, it is only used to extract the position. </summary>
        public RegionReader(Stream stream, string path, bool leaveOpen = false)
            : this(stream, 0, 0, leaveOpen)
        {
            (X, Z) = GetFilePos(path);
        }
        public RegionReader(Stream stream, int x, int z, bool leaveOpen = false)
        {
            Ensure.That(stream.CanSeek, "Stream must be seekable");

            stream.Position = 0;
            _s = new DataReader(stream, leaveOpen);
            if (_s.Length >= 8192) {
                _s.ReadBulkBE<int>(_locations);
            }
            X = x;
            Z = z;
        }

        /// <summary> Enumerates the NBT tag of all chunks in this region. </summary>
        public IEnumerable<(CompoundTag Tag, int X, int Z)> ReadAll()
        {
            //Read chunks sequentially to improve perf on slow medias (HDDs/whatever)
            //Don't really know how effective this is, hard to test because of caching.
            foreach (var (loc, i) in _locations.Select((v, i) => (v, i)).OrderBy(e => e.v)) {
                if (IsValidLocation(loc)) {
                    var tag = Read(loc);
                    yield return (tag, i & 31, i >> 5);
                }
            }
        }
        /// <summary> Enumerates the raw data of all chunks in this region. </summary>
        /// <remarks> The yielded ArraySegment is only valid until the next iteration, or the next call made in this instance. </remarks>
        public IEnumerable<(ArraySegment<byte> TagData, int X, int Z)> ReadAllData()
        {
            foreach (var (loc, i) in _locations.Select((v, i) => (v, i)).OrderBy(e => e.v)) {
                if (IsValidLocation(loc)) {
                    var data = ReadData(loc);
                    yield return (data, i & 31, i >> 5);
                }
            }
        }
        private bool IsValidLocation(int loc)
        {
            return (loc & 0xFF) != 0;
        }

        private CompoundTag Read(int loc)
        {
            var data = ReadData(loc);
            var mem = new MemoryStream(data.Array, data.Offset, data.Count);
            return NbtIO.Read(mem);
        }
        private ArraySegment<byte> ReadData(int loc)
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
            var data = _deflater.AllocInBuffer(actualLen);
            _s.ReadBytes(data);
            return Decompress(compressionType, data);
        }

        private ArraySegment<byte> Decompress(byte compressionType, ArraySegment<byte> data)
        {
            if (compressionType == 3) {
                return data;
            }
            var flavor = compressionType switch {
                1 => DeflateFlavor.Gzip,
                2 => DeflateFlavor.Zlib,
                _ => throw new NotSupportedException($"Chunk compression method {compressionType}")
            };
            return _deflater.Decompress(data, flavor);
        }

        private static int GetIndex(int x, int z)
        {
            Ensure.InRange(x, z, 0, 31, "Invalid region chunk coords");
            return x + z * 32;
        }

        /// <summary> Parses the region position from the specified path. </summary>
        public static (int X, int Z) GetFilePos(string path)
        {
            var m = Regex.Match(Path.GetFileName(path), @"^r\.(-?\d+)\.(-?\d+)\.mca$");
            if (!m.Success) {
                throw new FormatException("Region filename must have the form of 'r.0.0.mca'.");
            }
            int x = int.Parse(m.Groups[1].Value) * 32;
            int z = int.Parse(m.Groups[2].Value) * 32;
            return (x, z);
        }

        public void Dispose()
        {
            _s.Dispose();
            _deflater.Dispose();
        }
    }
}
