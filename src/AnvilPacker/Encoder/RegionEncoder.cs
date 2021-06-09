using System;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder
{
    public class RegionEncoder
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        private RegionBuffer _region;
        private int _blockCount; //updated by WriteChunkBitmap()
        private BlockCodec _blockCodec;

        public RegionEncoder(RegionBuffer region)
        {
            _region = region;
            _blockCodec = new v1.BlockCodecV1(region);
        }

        public void Encode(DataWriter stream, IProgress<double> progress = null)
        {
            var sw = Stopwatch.StartNew();

            var headerBuf = new MemoryDataWriter(1024 * 256);
            using (var comp = Compressors.NewBrotliEncoder(headerBuf, true, 9, 22)) {
                WriteHeader(comp);
                WriteMetadata(comp);
            }

            var headerSpan = headerBuf.BufferSpan;
            stream.WriteVarUInt(1); //data version
            stream.WriteIntLE(headerSpan.Length);
            stream.WriteBytes(headerSpan);

            long headerTime = sw.ElapsedMilliseconds;
            sw.Restart();

            long blocksStartPos = stream.Position;
            _blockCodec.Encode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));

            long blockEncTime = sw.ElapsedMilliseconds;
            sw.Restart();

            double bitsPerBlock = (stream.Position - blocksStartPos) * 8.0 / _blockCount;

            _logger.Debug($"Stats for region {_region.X >> 5} {_region.Z >> 5}");
            _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M  Palette: {_region.Palette.Count}");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}");
            _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB  Meta: {headerSpan.Length / 1024.0:0.000}KB");
            _logger.Debug($" EncTime: Meta: {headerTime}ms  Blocks: {blockEncTime}ms  Speed: {_blockCount / 1000.0 / blockEncTime:0.0}m blocks/sec");
        }

        private void WriteHeader(DataWriter stream)
        {
            stream.WriteVarInt(Maths.FloorDiv(_region.X, _region.Size));
            stream.WriteVarInt(Maths.FloorDiv(_region.Z, _region.Size));

            WritePalette(stream);
            WriteChunkBitmap(stream);

            stream.WriteVarUInt(_blockCodec.GetId());
            _blockCodec.WriteSettings(stream);
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
                        _blockCount += exists ? 4096 : 0;
                    }
                }
            }
        }
        private void WritePalette(DataWriter stream)
        {
            var palette = _region.Palette;
            stream.WriteVarUInt(palette.Count);

            foreach (var state in palette) {
                int flags = 0;
                flags |= state.HasAttrib(BlockAttributes.Legacy) ? 1 << 0 : 0;
                stream.WriteByte(flags);
                
                stream.WriteNulString(state.ToString());
                stream.WriteNulString(state.Material.Name.ToString(false));

                stream.WriteVarUInt((int)(state.Attributes & ~BlockAttributes.InternalMask));
                stream.WriteByte(state.Emittance << 4 | state.Opacity);
                
                if (state.HasAttrib(BlockAttributes.Legacy)) {
                    stream.WriteVarUInt(state.Id);
                }
                if (state.Block.IsDynamic) {
                    _logger.Warn("Dynamic block in region {0} {1}. Decoder may generate innacurate lighting/heightmaps.", _region.X >> 5, _region.Z >> 5);
                }
            }
        }
        private void WriteMetadata(DataWriter stream)
        {
            stream.WriteVarUInt(0); //version

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                stream.WriteVarUInt((int)chunk.Flags);
                stream.WriteVarUInt(chunk.DataVersion);
                NbtIO.Write(chunk.Opaque, stream);
            }
            //pnbt: 39.156KB, nbt: 42.892KB
        }
    }
}
