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
    //based on a hash generated using neighbor blocks. The context contains a palette,
    //which will be used by a move to front transform. The index of the block in the palette
    //is encoded using adaptive binary arithmetic coding. Last, the block is moved to the
    //front of the context's palette (MTF).

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
        private DictionarySlim<BlockState, BlockId> _palette = new();
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

            _logger.Debug($"Stats for region {_region.X} {_region.Z}");
            _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M Palette: {_palette.Count}");
            _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB Overhead: {headerLen / 1024.0:0.000}KB");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}");
            _logger.Debug($" PredAccuracy: {_predAccuracySum * 100.0 / _blockCount:0.0}% NumContexts: 2^{CTX_BITS}");
        }

        private void EncodeChunks(DataWriter stream)
        {
            var contexts = new Context[1 << CTX_BITS];
            var ac = new ArithmEncoder(stream);
            var paletteCache = new (BlockId[] Palette, int Y)[_region.Size, _region.Size];

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(_region)) {
                var palette = GetCachedPalette(paletteCache, chunk);

                if (chunk.X % 32 == 0) {
                    _logger.Trace($"Encoding chunk {chunk.X} {chunk.Y} {chunk.Z}");
                }
                EncodeBlocks(chunk, y, palette, contexts, CTX_NEIGHBORS, ac);

                _blockCount += 16 * 16;
            }
            ac.Flush();
        }

        private unsafe void EncodeBlocks(ChunkIterator chunk, int y, BlockId[] palette, Context[] contexts, Vec3i[] neighbors, ArithmEncoder ac)
        {
            Debug.Assert(neighbors.Length <= ContextKey.MAX_SAMPLES);

            var key = new ContextKey();

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {

                    for (int i = 0; i < neighbors.Length; i++) {
                        var rel = neighbors[i];
                        int nx = x + rel.X;
                        int ny = y + rel.Y;
                        int nz = z + rel.Z;

                        if ((uint)(nx | ny | nz) < 16u) {
                            key.s[i] = GetBlockId(nx, ny, nz);
                        } else {
                            key.s[i] = GetInterBlockId(nx, ny, nz);
                        }
                    }
                    var ctx = GetContext(contexts, in key);
                    var id = GetBlockId(x, y, z);

                    int delta = ctx.PredictForward(id);
                    ctx.Nz.Write(ac, delta, 0, ctx.Palette.Length - 1);

                    _predAccuracySum += delta == 0 ? 1 : 0;
                }
            }
            ushort GetBlockId(int x, int y, int z)
            {
                return palette[chunk.GetBlockIdFast(x, y, z)];
            }
            ushort GetInterBlockId(int x, int y, int z)
            {
                var block = chunk.GetInterBlock(x, y, z);
                return _palette.GetOrAdd(block, (BlockId)_palette.Count);
            }
        }

        private BlockId[] GetCachedPalette((BlockId[] Palette, int Y)[,] cache, ChunkIterator chunk)
        {
            int rx = _region.X * _region.Size;
            int rz = _region.Z * _region.Size;
            ref var cached = ref cache[chunk.X - rx, chunk.Z - rz];

            if (cached.Palette != null && cached.Y == chunk.Y) {
                return cached.Palette;
            }
            var palette = new BlockId[chunk.Palette.Count];
            for (int i = 0; i < palette.Length; i++) {
                var block = chunk.Palette.GetState((BlockId)i);

                if (!_palette.TryGetValue(block, out var id)) {
                    id = (BlockId)_palette.Count;
                    _palette.Add(block, id);
                }
                palette[i] = id;
            }
            cached = (palette, chunk.Y);
            return palette;
        }
        private Context GetContext(Context[] contexts, in ContextKey key)
        {
            int slot = key.GetSlot(CTX_BITS);
            var ctx = contexts[slot] ??= new Context();

            //resize palette if necessary
            int oldLen = ctx.Palette.Length;
            int newLen = _palette.Count;
            if (oldLen < newLen) {
                Array.Resize(ref ctx.Palette, newLen);
                for (int i = oldLen; i < newLen; i++) {
                    ctx.Palette[i] = (BlockId)i;
                }
            }
            return ctx;
        }

        private void WriteHeader(DataWriter stream)
        {
            stream.WriteUShortBE(1); //data version

            stream.WriteByte(_region.Size / 32);

            WritePalette(stream);
            WriteChunkBitmap(stream);
        }

        private void WriteChunkBitmap(DataWriter stream)
        {
            //find Y extents
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var chunk in _region.Chunks) {
                if (chunk == null) continue;

                for (int y = chunk.MinSectionY; y <= chunk.MaxSectionY; y++) {
                    if (chunk.GetSection(y) != null) {
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }
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
            stream.WriteVarInt(minY);
            stream.WriteVarInt(maxY);

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
            var palette = _palette;
            stream.WriteVarInt(palette.Count);

            int expectedId = 0;
            foreach (var (block, id) in palette) {
                Debug.Assert(id == expectedId++);

                var name = Encoding.UTF8.GetBytes(block.ToString());
                stream.WriteVarInt(name.Length);
                stream.WriteBytes(name);

                stream.WriteVarInt((int)block.Attributes);
                stream.WriteByte(0); //opacity << 4 | emittance
            }
        }
    }
}
