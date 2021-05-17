using System;
using System.Diagnostics;
using System.IO;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder.v1
{
    public class Decoder
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private RegionBuffer _region;
        private int _blockCount; //updated by ReadChunkBitmap()

        private int _ctxBits;
        private Vec3i[] _ctxNeighbors;

        public Decoder(RegionBuffer region)
        {
            _region = region;
            region.Clear();
        }

        public void Decode(DataReader stream, IProgress<double> progress = null)
        {
            int version = stream.ReadVarUInt();
            Ensure.That(version == 1, "Unsupported version " + version);

            int headerLen = stream.ReadIntLE();
            var header = stream.ReadBytes(headerLen);
            using (var comp = Compressors.NewBrotliDecoder(new MemoryStream(header), true)) {
                ReadHeader(comp);
                ReadMetadata(comp);
            }
            DecodeChunks(stream, progress);
        }

        private void DecodeChunks(DataReader stream, IProgress<double> progress)
        {
            var contexts = new Context[1 << _ctxBits];
            var ac = new ArithmDecoder(stream);
            int blocksProcessed = 0;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(_region)) {
                DecodeBlocks(chunk, y, contexts, _ctxNeighbors, ac);

                blocksProcessed += 16 * 16;
                if ((blocksProcessed & 4095) == 0) { //update progress on every chunk
                    progress?.Report(blocksProcessed / (double)_blockCount);
                }
            }
        }
        private unsafe void DecodeBlocks(ChunkIterator chunk, int y, Context[] contexts, Vec3i[] neighbors, ArithmDecoder ac)
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
                    var ctx = GetContext(contexts, ref key);
                    var id = ctx.Read(ac);

                    chunk.SetBlockId(x, y, z, id);
                }
            }
        }
        private Context GetContext(Context[] contexts, ref ContextKey key)
        {
            int slot = key.GetSlot(_ctxBits);
            var ctx = contexts[slot] ??= new Context(_region.Palette);

            return ctx;
        }

        private void ReadHeader(DataReader stream)
        {
            _region.X = stream.ReadVarInt() * _region.Size;
            _region.Z = stream.ReadVarInt() * _region.Size;

            _ctxBits = stream.ReadByte();
            _ctxNeighbors = new Vec3i[stream.ReadByte()];
            for (int i = 0; i < _ctxNeighbors.Length; i++) {
                _ctxNeighbors[i] = new Vec3i(
                    stream.ReadSByte(),
                    stream.ReadSByte(),
                    stream.ReadSByte()
                );
            }
            ReadPalette(stream);
            ReadChunkBitmap(stream);
        }

        private void ReadChunkBitmap(DataReader stream)
        {
            int minY = stream.ReadVarInt();
            int maxY = stream.ReadVarInt();

            //chunk bitmap
            for (int z = 0; z < _region.Size; z++) {
                for (int x = 0; x < _region.Size; x++) {
                    if (stream.ReadByte() != 0) {
                        var chunk = new Chunk(_region.X + x, _region.Z + z, minY, maxY, _region.Palette);
                        _region.SetChunk(x, z, chunk);
                    }
                }
            }
            //section bitmap
            _blockCount = 0;
            for (int y = minY; y <= maxY; y++) {
                for (int z = 0; z < _region.Size; z++) {
                    for (int x = 0; x < _region.Size; x++) {
                        var chunk = _region.GetChunk(x, z);
                        if (chunk == null) continue;

                        if (stream.ReadByte() != 0) {
                            chunk.GetOrCreateSection(y);
                            _blockCount += 4096;
                        }
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
                string material = stream.ReadNulString();

                int flags = stream.ReadByte();
                var attribs = (BlockAttributes)stream.ReadVarUInt();
                byte light = stream.ReadByte();
                int emittance = light >> 4;
                int opacity = light & 15;

                BlockState block;
                if ((flags & (1 << 0)) != 0) {
                    int legacyId = stream.ReadVarUInt();
                    block = BlockRegistry.GetLegacyState(legacyId);
                } else {
                    block = BlockRegistry.ParseState(name);
                }
                Ensure.That(
                    block.Attributes == attribs &&
                    block.Material.Name == material &&
                    block.Emittance == emittance &&
                    block.Opacity == opacity,
                    "Dynamic block states not supported yet."
                );
                palette.Add(block);
            }
            _region.Palette = palette;
        }
        private void ReadMetadata(DataReader stream)
        {
            int version = stream.ReadVarUInt();
            Ensure.That(version == 0, "Unsupported version");

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                chunk.DataVersion = stream.ReadVarUInt();
                chunk.HasLightData = false;
                chunk.Opaque = NbtIO.Read(stream);
            }
        }
    }
}
