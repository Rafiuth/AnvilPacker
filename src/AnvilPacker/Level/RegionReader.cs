using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    //TODO: support for external chunks aka '.mcc' files
    //https://minecraft.gamepedia.com/Region_file_format
    public class RegionReader : IDisposable
    {
        private readonly DataReader _s;

        public RegionReader(string filename)
        {
            _s = new DataReader(File.OpenRead(filename));
        }

        public IEnumerable<(CompoundTag Tag, int X, int Z)> ReadAll()
        {
            if (_s.Length < 8192) {
                yield break; //file header is missing
            }
            var locations = new int[1024];
            _s.Position = 0;
            _s.ReadBulkBE<int>(locations);
            
            for (int z = 0; z < 32; z++) {
                for (int x = 0; x < 32; x++) {
                    var tag = Read(locations[GetIndex(x, z)]);
                    if (tag != null) {
                        yield return (tag, x, z);
                    }
                }
            }
        }
        public CompoundTag Read(int x, int z)
        {
            _s.Position = GetIndex(x, z) * 4;
            int loc = _s.ReadIntBE();
            return Read(loc);
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

            using var dataStream = compressionType switch {
                1 => new GZipStream(rawStream, CompressionMode.Decompress),
                2 => new_ZlibStream(rawStream, CompressionMode.Decompress),
                3 => rawStream,
                _ => throw new NotSupportedException($"Chunk compression method {compressionType}")
            };
            return NbtIO.Read(new DataReader(dataStream));
        }

        private static Stream new_ZlibStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        {
            //adapted from here: https://github.com/dotnet/runtime/issues/38022#issuecomment-645612109
            var argTypes = new[] { typeof(Stream), typeof(CompressionMode), typeof(bool), typeof(int), typeof(long) };
            var ctor = typeof(DeflateStream).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, argTypes, null);

            return (DeflateStream)ctor.Invoke(new object[] { stream, mode, leaveOpen, 15, -1L });
        }

        private static int GetIndex(int x, int z)
        {
            Ensure.InRange(x, z, 0, 31, "Invalid region chunk coords");
            return x + z * 32;
        }

        public void Dispose()
        {
            _s.Dispose();
        }
    }
}
