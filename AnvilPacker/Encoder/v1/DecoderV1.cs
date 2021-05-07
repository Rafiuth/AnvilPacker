using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    public class DecoderV1
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private RegionBuffer _region;
        private int _blockCount; //updated by ReadChunkBitmap()

        private int _ctxBits;
        private Vec3i[] _ctxNeighbors;

        public RegionBuffer Decode(DataReader stream, Action<double> progress = null)
        {
            int version = stream.ReadVarUInt();
            Ensure.That(version == 1, "Unsupported version " + version);

            int headerLen = stream.ReadIntLE();
            var header = stream.ReadBytes(headerLen);
            using (var comp = Compressors.NewBrotliDecoder(new MemoryStream(header), true)) {
                ReadHeader(comp);
                ReadOpaqueTags(comp);
            }
            DecodeChunks(stream, progress);

            return _region;
        }

        private void DecodeChunks(DataReader stream, Action<double> progress)
        {
            var contexts = new Context[1 << _ctxBits];
            var ac = new ArithmDecoder(stream);
            int blocksProcessed = 0;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(_region)) {
                DecodeBlocks(chunk, y, contexts, _ctxNeighbors, ac);

                blocksProcessed += 16 * 16;
                if (chunk.X % 32 == 0) {
                    progress?.Invoke(blocksProcessed / (double)_blockCount);
                }
            }
        }
        private unsafe void DecodeBlocks(ChunkIterator chunk, int y, Context[] contexts, Vec3i[] neighbors, ArithmDecoder ac)
        {
            Debug.Assert(neighbors.Length <= ContextKey.MAX_SAMPLES);
            Debug.Assert(chunk.Palette == _region.Palette);

            var key = new ContextKey();

            //using var sw = new StreamWriter("decode.txt", true);

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
                    int delta = ctx.Nz.Read(ac, 0, ctx.Palette.Length - 1);
                    var id = ctx.PredictBackward(delta);

                    chunk.SetBlockId(x, y, z, id);

                    //sw.WriteLine($"Dec {key.GetSlot(_ctxBits)} {id} {delta}");
                }
            }
        }
        private Context GetContext(Context[] contexts, in ContextKey key)
        {
            int slot = key.GetSlot(_ctxBits);
            var ctx = contexts[slot] ??= new Context(_region.Palette);

            return ctx;
        }

        private void ReadHeader(DataReader stream)
        {
            _region = new RegionBuffer(stream.ReadByte());
            _region.X = stream.ReadVarInt() * _region.Size;
            _region.Z = stream.ReadVarInt() * _region.Size;

            _ctxBits = stream.ReadByte();
            _ctxNeighbors = new Vec3i[stream.ReadByte()];
            for (int i = 0; i < _ctxNeighbors.Length; i++) {
                int v = stream.ReadByte();
                //01 234 56  bit
                //xx yyy zz  field
                _ctxNeighbors[i] = new Vec3i(
                    (v << 30) >> 30, 
                    (v << 27) >> 29,
                    (v << 25) >> 30
                );
            }
            ReadPalette(stream);
            ReadChunkBitmap(stream);
        }

        private void ReadChunkBitmap(DataReader stream)
        {
            int minY = stream.ReadVarInt();
            int maxY = stream.ReadVarInt();

            _blockCount = 0;
            for (int y = minY; y <= maxY; y++) {
                for (int z = 0; z < _region.Size; z++) {
                    for (int x = 0; x < _region.Size; x++) {
                        if (stream.ReadByte() == 0) continue;

                        var chunk = _region.GetChunk(x, z);
                        if (chunk == null) {
                            chunk = new Chunk(_region.X + x, _region.Z + z, _region.Palette);
                            _region.SetChunk(x, z, chunk);
                        }
                        chunk.SetSection(y, new ChunkSection(chunk, y));
                        _blockCount += 4096;
                    }
                }
            }
        }
        private void ReadPalette(DataReader stream)
        {
            int count = stream.ReadVarUInt();
            var palette = new BlockPalette(count);

            for (int i = 0; i < count; i++) {
                string name = stream.ReadNulString();

                var attribs = (BlockAttributes)stream.ReadVarUInt();
                byte light = stream.ReadByte();
                int emittance = light >> 4;
                int opacity = light & 15;

                var block = BlockState.Parse(name);
                //TODO: this
                Ensure.That(block.Attributes == attribs, "Dynamic block states not supported yet.");
                palette.Add(block);
            }
            _region.Palette = palette;
        }
        private void ReadOpaqueTags(DataReader stream)
        {
            int version = stream.ReadVarUInt();
            Ensure.That(version == 0, "Unsupported version");

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                chunk.Opaque = NbtIO.Read(stream);
            }
        }
    }
}
