using System;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
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
        private EstimatedBlockAttribs _estimAttribs = new();

        public RegionEncoder(RegionBuffer region)
        {
            _region = region;
            _blockCodec = new v1.BlockCodecV1(region);
        }

        public void Encode(DataWriter stream, IProgress<double> progress = null)
        {
            var sw = Stopwatch.StartNew();

            _estimAttribs.Estimate(_region);
            long attribEstimTime = sw.ElapsedMilliseconds;

            sw.Restart();

            var headerBuf = new MemoryDataWriter(1024 * 256);
            using (var comp = Compressors.NewBrotliEncoder(headerBuf, true, 9, 22)) {
                WriteHeader(comp);
                WriteMetadata(comp);
            }

            var headerSpan = headerBuf.BufferSpan;
            WriteSyncTag(stream, "main", 0);
            stream.WriteIntLE(headerSpan.Length);
            stream.WriteBytes(headerSpan);

            long headerTime = sw.ElapsedMilliseconds;
            sw.Restart();

            long blocksStartPos = stream.Position;
            _blockCodec.Encode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));

            long blockEncTime = sw.ElapsedMilliseconds;

            if (_logger.IsDebugEnabled) {
                double bitsPerBlock = (stream.Position - blocksStartPos) * 8.0 / _blockCount;

                _logger.Debug($"Encoder stats @ {_region}");
                _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M | PaletteSize: {_region.Palette.Count}");
                _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB | Meta: {headerSpan.Length / 1024.0:0.000}KB | BitsPerBlock: {bitsPerBlock:0.000}");
                _logger.Debug($" Times: AttribEstim: {attribEstimTime}ms | Meta: {headerTime}ms | Blocks: {blockEncTime}ms");
                _logger.Debug($" Speed: {_blockCount / 1000.0 / blockEncTime:0.0}M blocks/sec");
            }
        }

        private void WriteHeader(DataWriter stream)
        {
            stream.WriteVarInt(_region.X >> 5);
            stream.WriteVarInt(_region.Z >> 5);

            WritePalette(stream); //no deps
            WriteHeightmapAttribs(stream); //depends on palette
            WriteLightAttribs(stream);
            WriteChunkBitmap(stream); //no deps

            stream.WriteVarUInt(_blockCodec.GetId());
            _blockCodec.WriteSettings(stream);
        }

        private void WriteChunkBitmap(DataWriter stream)
        {
            WriteSyncTag(stream, "cmap", 0);
            
            var (minY, maxY) = _region.GetChunkYExtents();

            stream.WriteVarInt(minY);
            stream.WriteVarInt(maxY);

            //design note: using a whole byte per bit because brotli works best that way.
            //chunk bitmap
            for (int z = 0; z < _region.Size; z++) {
                for (int x = 0; x < _region.Size; x++) {
                    bool exists = _region.GetChunk(x, z) != null;
                    stream.WriteBool(exists);
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

                        stream.WriteBool(exists);
                        _blockCount += exists ? 4096 : 0;
                    }
                }
            }
        }
        private void WritePalette(DataWriter stream)
        {
            WriteSyncTag(stream, "plte", 0);

            var palette = _region.Palette;
            stream.WriteVarUInt(palette.Count);

            foreach (var state in palette) {
                var flags = BlockFlagsEx.FromState(state);
                stream.WriteVarUInt((int)flags);

                if (flags.HasFlag(BlockFlags.Legacy)) {
                    stream.WriteVarUInt(state.Id);
                } else {
                    stream.WriteNulString(state.ToString());
                }
            }
        }

        private void WriteHeightmapAttribs(DataWriter stream)
        {
            WriteSyncTag(stream, "hmap", 0);

            var attribs = _estimAttribs.HeightmapAttribs;
            stream.WriteVarUInt(attribs.OpacityMap.Count);

            foreach (var (type, isOpaque) in attribs.OpacityMap) {
                Ensure.That(isOpaque.Length == _region.Palette.Count);

                stream.WriteNulString(type);

                for (int i = 0; i < isOpaque.Length; i++) {
                    stream.WriteBool(isOpaque[i]);
                }
            }
        }
        private void WriteLightAttribs(DataWriter stream)
        {
            WriteSyncTag(stream, "lght", 0);
        }

        private void WriteMetadata(DataWriter stream)
        {
            WriteSyncTag(stream, "meta", 0);
            NbtIO.Write(_region.ExtraData ?? new(), stream);

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                stream.WriteVarUInt((int)chunk.Flags);
                stream.WriteVarUInt(chunk.DataVersion);
                NbtIO.Write(chunk.Opaque, stream);
            }
            //pnbt: 39.156KB, nbt: 42.892KB
        }

        private void WriteSyncTag(DataWriter stream, string id, byte version)
        {
            for (int i = 0; i < 4; i++) {
                stream.WriteByte(id[i]);
            }
            stream.WriteByte(version);
        }
    }
}
