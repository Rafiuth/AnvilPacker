using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    //Benchmarks:
    // meth    size     notes
    // raw    20396KB  1 byte per block, YZX order. chunk size is 16^3, empty chunks are not included.
    //BZip2   2038KB
    //Deflate 1864KB
    //LZMA2   1569KB
    //APv1    1280KB   block data only.
    //paq8l   1225KB   very slow, -5 took ~20min.
    public class EncoderV1
    {
        private const int CTX_BITS = 13;
        private static readonly Vec3i[] CTX_NEIGHBORS = {
            //new(-1, 0, 0),
            new(0, -1, 0),
            new(0, 0, -1),
            new(-1, 1, 0),
        };

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        private RegionBuffer _region;
        private int _predAccuracySum, _blockCount;

        public EncoderV1(RegionBuffer region)
        {
            _region = region;
        }

        public void Encode(DataWriter stream)
        {
            var buf = new MemoryDataWriter(1024 * 1024 * 4);
            EncodeChunks(buf);

            long startPos = stream.Position;
            WriteHeader(stream);
            long headerLen = stream.Position - startPos;

            stream.WriteBytes(buf.BufferSpan);

            double bitsPerBlock = buf.Position * 8.0 / _blockCount;

            buf.Clear();
            EncodeHeightMaps(buf);

            stream.WriteBytes(buf.BufferSpan);

            _logger.Debug($"Stats for region {_region.X} {_region.Z}");
            _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M Palette: {_region.Palette.Count}");
            _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB Overhead: {headerLen / 1024.0:0.000}KB");
            _logger.Debug($" EncHeightmaps: {buf.Position / 1024.0:0.000}KB");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}");
            _logger.Debug($" PredAccuracy: {_predAccuracySum * 100.0 / _blockCount:0.0}% NumContexts: 2^{CTX_BITS}");
        }

        private void EncodeChunks(DataWriter stream)
        {
            var contexts = new Context[1 << CTX_BITS];
            var ac = new ArithmEncoder(stream);

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(_region)) {
                if (y == 0 && chunk.X % 32 == 0) {
                    _logger.Trace($"Encoding chunk {chunk.X} {chunk.Y} {chunk.Z}");
                }
                EncodeBlocks(chunk, y, contexts, CTX_NEIGHBORS, ac);

                _blockCount += 16 * 16;
            }
            ac.Flush();
        }

        private unsafe void EncodeBlocks(ChunkIterator chunk, int y, Context[] contexts, Vec3i[] neighbors, ArithmEncoder ac)
        {
            Debug.Assert(neighbors.Length <= ContextKey.MAX_SAMPLES);
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
                    var ctx = GetContext(contexts, in key);
                    var id = chunk.GetBlockIdFast(x, y, z);

                    int delta = ctx.PredictForward(id);
                    ctx.Nz.Write(ac, delta, 0, ctx.Palette.Length - 1);

                    _predAccuracySum += delta == 0 ? 1 : 0;
                }
            }
        }
        private Context GetContext(Context[] contexts, in ContextKey key)
        {
            int slot = key.GetSlot(CTX_BITS);
            var ctx = contexts[slot] ??= new Context(_region.Palette);

            return ctx;
        }

        private void EncodeHeightMaps(DataWriter stream)
        {
            _logger.Trace("Encoding height maps...");
            var pred = new short[16 * 16];
            var types = _region.Chunks
                               .ExceptNull()
                               .SelectMany(c => c.HeightMaps.Select(h => h.Key))
                               .Distinct();

            var (minSy, maxSy) = _region.GetChunkYExtents();
            //rough estimate of min and max deltas.
            int minDelta = (minSy - maxSy) * 16;
            int maxDelta = (maxSy - minSy) * 16;

            foreach (var type in types) {
                stream.WriteString(type.Name, stream.WriteVarUInt);
            }
            stream.WriteVarInt(minDelta);
            stream.WriteVarInt(maxDelta);

            if (minDelta == 0 && maxDelta == 0) {
                //all predicted heights are correct, don't waste resources encoding them.
                return;
            }
            var ac = new ArithmEncoder(stream);
            var pSkip = new BitChance(0.9);
            var nzHeight = new NzCoder();

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                foreach (var type in types) {
                    var map = chunk.HeightMaps.Get(type);

                    pSkip.Write(ac, map == null);
                    if (map != null) {
                        HeightMaps.Calculate(chunk, type, pred);
                        for (int i = 0; i < 16 * 16; i++) {
                            nzHeight.Write(ac, map[i] - pred[i], minDelta, maxDelta);
                        }
                    }
                }
            }
            ac.Flush();
        }

        private void WriteHeader(DataWriter stream)
        {
            stream.WriteUShortBE(1); //data version

            stream.WriteByte(_region.Size / 32);

            stream.WriteByte(CTX_BITS);
            stream.WriteByte(CTX_NEIGHBORS.Length);
            foreach (var (nx, ny, nz) in CTX_NEIGHBORS) {
                Ensure.That(
                    nx >= -3 && nx <= 0 && 
                    ny >= -4 && ny <= 3 && 
                    nz >= -3 && nz <= 0
                );
                stream.WriteByte(
                    (nx & 3) << 0 | //-3..0
                    (ny & 7) << 2 | //-4..3
                    (nz & 3) << 5   //-3..0
                );
            }
            WritePalette(stream);
            WriteChunkBitmap(stream);
        }

        private void WriteChunkBitmap(DataWriter stream)
        {
            var (minY, maxY) = _region.GetChunkYExtents();
            //create bitmap
            int length = (maxY - minY + 1) * (_region.Size * _region.Size);
            var bitmap = new BitArray(length);
            int i = 0;

            for (int y = minY; y <= maxY; y++) {
                for (int z = 0; z < _region.Size; z++) {
                    for (int x = 0; x < _region.Size; x++) {
                        bitmap[i++] = _region.GetSection(x, y, z) != null;
                    }
                }
            }

            //encode it
            //TODO: improve bitmap encoding (maybe)
            //worst cases:
            // - 1.16 at 32x16x32 chunks = 16K bits = 2KB
            // - 1.17 at 32x64x32 chunks = 64K bits = 8KB
            //RLE gives ~200 bytes at 32x5x32 (normal overworld)
            stream.WriteVarUInt(minY);
            stream.WriteVarUInt(maxY);

            var bw = new BitWriter(stream);
            CodecPrimitives.RunLengthEncode(
                length,
                compare: (i, j) => bitmap[i] == bitmap[j],
                writeLiteral: i => bw.WriteBit(bitmap[i]),
                writeRunLen: l => bw.WriteVLC(l)
            );
            bw.Flush();
        }
        private void WritePalette(DataWriter stream)
        {
            var palette = _region.Palette;
            stream.WriteVarUInt(palette.Count);

            foreach (var (block, id) in palette.BlocksAndIds()) {
                var name = Encoding.UTF8.GetBytes(block.ToString());
                stream.WriteVarUInt(name.Length);
                stream.WriteBytes(name);

                stream.WriteVarUInt((int)block.Attributes);
                stream.WriteByte(block.Emittance << 4 | block.Opacity);
            }
        }
    }
}
