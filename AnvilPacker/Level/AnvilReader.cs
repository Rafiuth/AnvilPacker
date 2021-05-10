using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    //TODO: support for external chunks aka '.mcc' files
    //https://minecraft.gamepedia.com/Region_file_format
    public class AnvilReader : IDisposable
    {
        private readonly FileDataReader _s;

        public AnvilReader(string filename)
        {
            _s = new FileDataReader(filename);
        }

        public CompoundTag Read(int x, int z)
        {
            var (offset, sectorCount) = ReadLocation(x, z);
            if (sectorCount == 0) {
                return null;
            }
            _s.Position = offset;
            int len = _s.ReadIntBE() - 1;
            byte compressionType = _s.ReadByte();

            if (len > sectorCount * 4096) {
                throw new InvalidDataException($"Corrupted chunk: declared length larger than sector count.");
            }
            var rawStream = _s.ForkStream(offset + 5, len);

            using var dataStream = compressionType switch {
                1 => new GZipStream(rawStream, CompressionMode.Decompress),
                2 => new_ZlibStream(rawStream, CompressionMode.Decompress),
                3 => rawStream,
                _ => throw new NotSupportedException($"Chunk compression method {compressionType}")
            };
            return NbtIO.Read(new DataReader(dataStream));
        }

        private (int Offset, int SectorCount) ReadLocation(int x, int z)
        {
            _s.Position = GetIndex(x, z) * 4;
            int loc = _s.ReadIntBE();
            int offset = (loc >> 8) * 4096;
            int sectors = loc & 0xFF;
            return (offset, sectors);
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
            return (x & 31) + (z & 31) * 32;
        }

        public void Dispose()
        {
            _s.Dispose();
        }
    }
}
