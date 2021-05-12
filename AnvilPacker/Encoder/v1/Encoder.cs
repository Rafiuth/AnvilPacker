using System;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder.v1
{
    //Version 1 uses a fixed size context table. For each block, a context is selected
    //based on a hash generated using neighbor blocks. The context contains a copy of the
    //region palette. The index of the block in the palette is encoded using adaptive binary
    //arithmetic coding, then the block is moved to the first index of the palette (MTF).
    public class Encoder
    {
        private const int CTX_BITS = 13;
        private static readonly Vec3i[] CTX_NEIGHBORS = {
            new(-1, 0, 0),
            new(0, -1, 0),
            new(0, 0, -1),
        };

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        private RegionBuffer _region;
        private int _blockCount; //updated by WriteChunkBitmap()

        public Encoder(RegionBuffer region)
        {
            _region = region;
        }

        public void Encode(DataWriter stream, IProgress<double> progress = null)
        {
            var headerBuf = new MemoryDataWriter(1024 * 256);
            using (var comp = Compressors.NewBrotliEncoder(headerBuf, true, 9, 22)) {
                WriteHeader(comp);
                WriteMetadata(comp);
            }

            var headerSpan = headerBuf.BufferSpan;
            stream.WriteVarUInt(1); //data version
            stream.WriteIntLE(headerSpan.Length);
            stream.WriteBytes(headerSpan);

            long startPos = stream.Position;
            EncodeChunks(stream, progress);
            double bitsPerBlock = (stream.Position - startPos) * 8.0 / _blockCount;

            _logger.Debug($"Stats for region {_region.X >> 5} {_region.Z >> 5}");
            _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M  Palette: {_region.Palette.Count}");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}  NumContexts: 2^{CTX_BITS}");
            _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB  Header+Metadata: {headerSpan.Length / 1024.0:0.000}KB");
        }

        private void EncodeChunks(DataWriter stream, IProgress<double> progress)
        {
            var contexts = new Context[1 << CTX_BITS];
            var ac = new ArithmEncoder(stream);
            int blocksProcessed = 0;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(_region)) {
                EncodeBlocks(chunk, y, contexts, CTX_NEIGHBORS, ac);

                blocksProcessed += 16 * 16;
                if ((blocksProcessed & 4095) == 0) { //update progress on every chunk
                    progress?.Report(blocksProcessed / (double)_blockCount);
                }
            }
            ac.Flush();
        }
        private unsafe void EncodeBlocks(ChunkIterator chunk, int y, Context[] contexts, Vec3i[] neighbors, ArithmEncoder ac)
        {
            Debug.Assert(neighbors.All(n => n.Y <= 0 && (n.X <= 0 || n.Z <= 0)));
            Debug.Assert(chunk.Palette == _region.Palette);

            var key = new ContextKey();

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {

                    for (int i = 0; i < neighbors.Length; i++) {
                        var rel = neighbors[i];
                        int nx = x + rel.X;
                        int ny = y + rel.Y;
                        int nz = z + rel.Z;

                        key.s[i] = chunk.GetBlockId(nx, ny, nz);
                    }
                    var ctx = GetContext(contexts, ref key);
                    var id = chunk.GetBlockIdFast(x, y, z);

                    ctx.Write(ac, id);
                }
            }
        }
        private Context GetContext(Context[] contexts, ref ContextKey key)
        {
            int slot = key.GetSlot(CTX_BITS);
            var ctx = contexts[slot] ??= new Context(_region.Palette);

            return ctx;
        }

        private void WriteHeader(DataWriter stream)
        {
            stream.WriteVarInt(Maths.FloorDiv(_region.X, _region.Size));
            stream.WriteVarInt(Maths.FloorDiv(_region.Z, _region.Size));

            stream.WriteByte(CTX_BITS);
            stream.WriteByte(CTX_NEIGHBORS.Length);
            foreach (var (nx, ny, nz) in CTX_NEIGHBORS) {
                stream.WriteSByte(nx);
                stream.WriteSByte(ny);
                stream.WriteSByte(nz);
            }
            WritePalette(stream);
            WriteChunkBitmap(stream);
        }

        private void WriteChunkBitmap(DataWriter stream)
        {
            var (minY, maxY) = _region.GetChunkYExtents();

            stream.WriteVarInt(minY);
            stream.WriteVarInt(maxY);

            //design note: using a whole byte per bit because brotli works best that way.
            //chunk bitmap
            for (int z = 0; z < _region.Size; z++) {
                for (int x = 0; x < _region.Size; x++) {
                    bool exists = _region.GetChunk(x, z) != null;
                    stream.WriteByte(exists ? 1 : 0);
                }
            }
            //section bitmap
            _blockCount = 0;
            for (int y = minY; y <= maxY; y++) {
                for (int z = 0; z < _region.Size; z++) {
                    for (int x = 0; x < _region.Size; x++) {
                        var chunk = _region.GetChunk(x, z);
                        if (chunk == null) continue;
                        
                        bool exists = chunk.GetSection(y) != null;
                        stream.WriteByte(exists ? 1 : 0);

                        if (exists) {
                            _blockCount += 4096;
                        }
                    }
                }
            }
        }
        private void WritePalette(DataWriter stream)
        {
            var palette = _region.Palette;
            stream.WriteVarUInt(palette.Count);

            foreach (var (block, id) in palette.BlocksAndIds()) {
                stream.WriteNulString(block.ToString());
                stream.WriteNulString(block.Material.Name.ToString(false));

                stream.WriteVarUInt((int)block.Attributes);
                stream.WriteByte(block.Emittance << 4 | block.Opacity);
            }
        }
        private void WriteMetadata(DataWriter stream)
        {
            stream.WriteVarUInt(0); //version

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                stream.WriteVarUInt(chunk.DataVersion);
                NbtIO.Write(chunk.Opaque, stream);
            }
            //pnbt: 39.156KB, nbt: 42.892KB
        }
    }
}
