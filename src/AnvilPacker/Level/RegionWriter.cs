using System;
using System.Diagnostics;
using System.IO;
using AnvilPacker.Data;
using AnvilPacker.Data.Nbt;
using AnvilPacker.Util;
using LibDeflate;

namespace AnvilPacker.Level
{
    //TODO: support for external chunks aka '.mcc' files
    /// <summary>A simple anvil region file writer.</summary>
    /// <remarks> <para/>
    /// - This class only supports creating files, not modifying existing ones. <br/>
    /// - A chunk can only be written once, do not call <see cref="Write(int, int, CompoundTag)"/> with the same chunk twice or it will throw an exception. <br/>
    /// - When you are done writing the region, call <see cref="WriteHeader"/> or dispose this object
    /// to ensure the file header is written otherwise you will endup with an corrupted file.
    /// </remarks>
    public class RegionWriter : IDisposable
    {
        private const int MAX_COMPRESSED_CHUNK_SIZE = 256 * 4096; //1MB / .mca hard limit

        private readonly DataWriter _s;
        private readonly bool _leaveOpen;
        // Chunk index table. each entry is encoded as: `sectorId << 8 | sectorCount`
        private int[] _locations = new int[1024];
        private bool _headerDirty = true;

        private MemoryDataWriter _chunkBuf = new(1024 * 256);
        private byte[] _compBuf = new byte[1024 * 64];
        private ZlibCompressor _zlibEnc = new(6);

        public RegionWriter(string dir, int rx, int rz)
            : this(Path.Combine(dir, $"r.{rx}.{rz}.mca"))
        {
        }
        public RegionWriter(string filename)
            : this(File.Create(filename))
        {
        }
        public RegionWriter(Stream stream, bool leaveOpen = false)
        {
            _s = new DataWriter(stream);
            _s.Position = 8192;
            _leaveOpen = leaveOpen;
        }

        public void WriteHeader()
        {
            if (!_headerDirty) return;
            _headerDirty = false;

            _s.Position = 0;
            for (int i = 0; i < 1024; i++) {
                _s.WriteIntBE(_locations[i]);
            }
            //Write timestamps (unused by Minecraft)
            int timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < 1024; i++) {
                _s.WriteIntBE(timestamp);
            }
            //Older versions will corrupt the location table if the file size isn't a multiple of 4096 bytes.
            //Setting _s.Position isn't enough to change the file size.
            _s.Length = SectorAlign(_s.Length);
            _s.Position = _s.Length;
        }

        public bool Exists(int x, int z)
        {
            return _locations[GetIndex(x, z)] != 0;
        }

        public void Write(int x, int z, CompoundTag tag)
        {
            _chunkBuf.Clear();
            NbtIO.Write(tag, _chunkBuf);
            Write(x, z, _chunkBuf.BufferSpan);
        }
        public void Write(int x, int z, Span<byte> rawData)
        {
            ref int loc = ref _locations[GetIndex(x, z)];
            long startPos = _s.Position;

            Ensure.That(loc == 0, "Chunk can only be written once.");
            Debug.Assert(startPos % 4096 == 0, "File position should always be aligned to 4096 bytes.");

            var data = Compress(rawData);

            const int HDR_LEN = 5; //LEN:4 + CM:1
            loc = PackLocation(startPos, data.Length + HDR_LEN);

            _s.WriteIntBE(data.Length + 1); //length
            _s.WriteByte(2); //compressionMethod: zlib
            _s.WriteBytes(data);

            _s.Position = SectorAlign(_s.Position);
            _headerDirty = true;
        }

        private Span<byte> Compress(ReadOnlySpan<byte> data)
        {
            while (true) {
                int len = _zlibEnc.Compress(data, _compBuf);
                if (len > 0) {
                    return _compBuf.AsSpan(0, len);
                }
                int newBufLen = _compBuf.Length * 2;
                Ensure.That(newBufLen <= MAX_COMPRESSED_CHUNK_SIZE, "Chunk data larger than allowed (trying to expand buffer too much)");
                Array.Resize(ref _compBuf, newBufLen);
            }
        }

        private static int PackLocation(long offset, int length)
        {
            int startSector = (int)(offset / 4096);
            int numSectors = Maths.CeilDiv(length, 4096);

            Ensure.That(startSector <= 0xFFFFFF && numSectors <= 255, "Can't fit chunk in region");
            return startSector << 8 | numSectors;
        }
        private static long SectorAlign(long pos)
        {
            return (pos + 4095) & ~4095;
        }
        private static int GetIndex(int x, int z)
        {
            Ensure.InRange(x, z, 0, 31, "Invalid region chunk coords");
            return x + z * 32;
        }

        public void Dispose()
        {
            WriteHeader();
            if (!_leaveOpen) {
                _s.Dispose();
            }
            _chunkBuf.Dispose();
            _zlibEnc.Dispose();
        }
    }
}
