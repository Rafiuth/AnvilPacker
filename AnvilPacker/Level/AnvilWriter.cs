using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    //TODO: support for external chunks aka '.mcc' files
    /// <summary>A simple anvil region file writer.</summary>
    /// <remarks> <para/>
    /// - This class only supports creating files, not modifying existing files. <br/>
    /// - A chunk can only be written once, do not call <see cref="Write(int, int, CompoundTag)"/> with the same chunk twice or it will throw an exception. <br/>
    /// 
    /// - When you are done writing the region, call <see cref="WriteHeader"/> or dispose this object
    /// to ensure the file header is written otherwise you will endup with an corrupted file.
    /// </remarks>
    public class AnvilWriter : IDisposable
    {
        private readonly DataWriter _s;
        // Chunk index table. each entry is encoded as: `sectorId << 8 | sectorCount`
        private int[] _locations = new int[1024];
        private bool _headerDirty = true;

        public AnvilWriter(string path, int rx, int rz)
            : this(Path.Combine(path, $"r.{rx}.{rz}.mca"))
        {
        }
        public AnvilWriter(string filename)
        {
            //disabling buffer allows passing BaseStream without seeking twice
            _s = new DataWriter(File.Create(filename), false, 0);
            _s.Position = 8192;
        }

        public void WriteHeader()
        {
            if (!_headerDirty) {
                return;
            }
            _headerDirty = false;

            _s.Position = 0;
            for (int i = 0; i < 1024; i++) {
                _s.WriteIntBE(_locations[i]);
            }
            //Write timestamps (unused by Minecraft)
            int timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(); //only good for another 15 years lol.
            for (int i = 0; i < 1024; i++) {
                _s.WriteIntBE(timestamp);
            }

            _s.Position = _s.Length;
        }

        public bool Exists(int x, int z)
        {
            return _locations[GetIndex(x, z)] != 0;
        }

        public void Write(int x, int z, CompoundTag tag)
        {
            ref int loc = ref _locations[GetIndex(x, z)];
            if (loc != 0) {
                throw new NotImplementedException("Chunk can only be written once.");
            }

            long startPos = _s.Position;
            Debug.Assert(startPos % 4096 == 0, "File pointer should always be aligned to 4KB sectors.");

            const int HDR_LEN = 5;
            _s.Position = startPos + HDR_LEN;

            using (var stream = new DataWriter(new_ZlibStream(_s.BaseStream, CompressionLevel.Optimal, true))) {
                NbtIO.Write(tag, stream);
            }
            long endPos = _s.Position;
            int totalLen = (int)(endPos - startPos);
            loc = PackLocation(startPos, totalLen);

            endPos = Align(endPos);

            //go back and write header
            _s.Position = startPos;

            loc = PackLocation(startPos, totalLen);

            _s.WriteIntBE(totalLen - HDR_LEN + 1); //length
            _s.WriteByte(2); //compressionMethod: zlib

            _s.Position = endPos;

            _headerDirty = true;
        }

        private int PackLocation(long offset, int length)
        {
            int startSector = (int)(offset / 4096);
            int numSectors = Maths.CeilDiv(length, 4096);

            if (startSector > 0xFFFFFF || numSectors > 255) {
                throw new InvalidOperationException($"Cannot fit chunk in anvil region! Offset={offset} Len={length / 1024}KB");
            }
            return startSector << 8 | numSectors;
        }

        private long Align(long pos)
        {
            return (pos + 4095) & ~4095;
        }

        private static int GetIndex(int x, int z)
        {
            Ensure.InRange(x, z, 0, 31, "Invalid region chunk coords");
            return x + z * 32;
        }

        private static Stream new_ZlibStream(Stream stream, CompressionLevel level, bool leaveOpen = false)
        {
            //adapted from here: https://github.com/dotnet/runtime/issues/38022#issuecomment-645612109
            //https://github.com/dotnet/runtime/blob/master/src/libraries/System.IO.Compression/src/System/IO/Compression/DeflateZLib/DeflateStream.cs#L81
            //internal DeflateStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen, int windowBits)
            var argTypes = new[] { typeof(Stream), typeof(CompressionLevel), typeof(bool), typeof(int) };
            var ctor = typeof(DeflateStream).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, argTypes, null);

            return (DeflateStream)ctor.Invoke(new object[] { stream, level, leaveOpen, 15 });
        }

        public void Dispose()
        {
            WriteHeader();
            _s.Dispose();
        }
    }
}
