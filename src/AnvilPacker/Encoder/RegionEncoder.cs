#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
        private EstimatedHeightmapAttribs _estimHeightmapAttribs = new();
        private EstimatedLightAttribs _estimLightAttribs = new();

        private BlockCodec _blockCodec;
        private RegionEncoderSettings _settings;

        public RegionEncoder(RegionBuffer region, RegionEncoderSettings settings)
        {
            _region = region;
            _settings = settings;

            _blockCodec = settings.BlockCodec.Create(region);
        }

        public void Encode(DataWriter stream, IProgress<double>? progress = null)
        {
            var headerStats = WritePart(stream, "Header", true, dw => {
                WriteHeader(dw);
                WriteMetadata(dw);
            });
            var blockDataStats = WritePart(stream, "Blocks", false, dw => {
                _blockCodec.Encode(dw, CodecProgressListener.MaybeCreate(_blockCount, progress));
            });
            if (_settings.HeightmapEncMode != RepDataEncMode.Strip) {
                WritePart(stream, "Heightmaps", true, WriteHeightmaps);
            }
            if (_settings.LightEncMode != RepDataEncMode.Strip) {
                WritePart(stream, "Lighting", true, WriteLightData);
            }

            if (_logger.IsDebugEnabled) {
                double bitsPerBlock = blockDataStats.Length * 8.0 / _blockCount;

                _logger.Debug($"Encoder stats @ {_region}");
                _logger.Debug($" NumBlocks: {_blockCount / 1000000.0:0.0}M | PaletteSize: {_region.Palette.Count}");
                _logger.Debug($" EncSize: {stream.Position / 1024.0:0.000}KB | Meta: {headerStats.Length / 1024.0:0.000}KB | BitsPerBlock: {bitsPerBlock:0.000}");
                _logger.Debug($" Times: Meta: {headerStats.TimeMillis}ms | Blocks: {blockDataStats.TimeMillis}ms");
                _logger.Debug($" Speed: {_blockCount / 1000.0 / blockDataStats.TimeMillis:0.0}M blocks/sec");
            }
        }

        private void WriteHeader(DataWriter stream)
        {
            //Note: this is an arithmetic shift, using a div here would give wrong values.
            //regionX = floorDiv(chunkX / 32) = x >> 5
            stream.WriteVarInt(_region.X >> 5);
            stream.WriteVarInt(_region.Z >> 5);
            stream.WriteByte((byte)_settings.HeightmapEncMode);
            stream.WriteByte((byte)_settings.LightEncMode);

            //no deps
            WritePalette(stream);
            WriteChunkBitmap(stream);

            //depends on palette & header fields above
            if (_settings.HeightmapEncMode != RepDataEncMode.Normal) {
                WriteHeightmapAttribs(stream);
            }
            if (_settings.LightEncMode != RepDataEncMode.Normal) {
                WriteLightAttribs(stream);
            }
            //no deps
            stream.WriteVarUInt(_blockCodec.GetId());
            _blockCodec.WriteHeader(stream);
        }

        private void WriteChunkBitmap(DataWriter stream)
        {
            WriteSyncTag(stream, "ChunkBMP", 0);
            
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
            WriteSyncTag(stream, "Palette", 0);

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

        private void WriteMetadata(DataWriter stream)
        {
            WriteSyncTag(stream, "Meta", 0);
            NbtIO.Write(_region.ExtraData ?? new(), stream);

            foreach (var chunk in _region.ExistingChunks) {
                stream.WriteVarUInt((int)chunk.Flags);
                stream.WriteVarUInt(chunk.DataVersion);
                //pnbt: 39.156KB, nbt: 42.892KB
                NbtIO.Write(chunk.Opaque, stream);
            }
        }

        private void WriteHeightmapAttribs(DataWriter stream)
        {
            WriteSyncTag(stream, "HeightAttribs", 0);

            var attribs = _estimHeightmapAttribs;
            attribs.Estimate(_region);

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
            WriteSyncTag(stream, "LightAttribs", 0);

            var attribs = _estimLightAttribs;
            attribs.Estimate(_region);

            Ensure.That(attribs.LightAttribs.Length == _region.Palette.Count);

            foreach (var block in attribs.LightAttribs) {
                stream.WriteByte(block.Data);
            }
        }

        private void WriteHeightmaps(DataWriter stream)
        {
            WriteSyncTag(stream, "Heightmaps", 0);

            var types = new Dictionary<string, (int Mask, HeightmapComputer? Computer)>();
            var predHeightmap = new Heightmap();
            bool deltaEnc = _settings.HeightmapEncMode == RepDataEncMode.Delta;

            //Find existing heightmap types
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var (type, heightmap) in chunk.Heightmaps) {
                    if (types.ContainsKey(type)) continue;
                    Ensure.That(types.Count <= 32, "Can't have more than 32 heightmap types.");

                    HeightmapComputer? computer = null;

                    if (deltaEnc) {
                        var opacityMap = _estimHeightmapAttribs.OpacityMap[type];
                        computer = new HeightmapComputer(_region, type, opacityMap);
                    }
                    types[type] = (1 << types.Count, computer);
                }
            }

            //Types
            stream.WriteVarUInt(types.Count);
            foreach (var type in types.Keys) {
                stream.WriteNulString(type);
            }

            //Bitmap
            foreach (var chunk in _region.ExistingChunks) {
                int mask = 0;
                foreach (var (type, heightmap) in chunk.Heightmaps) {
                    mask |= types[type].Mask;
                }
                stream.WriteVarUInt(mask);
            }

            //Payload
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var (type, data) in types) {
                    if (!chunk.Heightmaps.TryGetValue(type, out var heightmap)) continue;

                    if (deltaEnc) {
                        data.Computer!.Compute(chunk, predHeightmap);
                    }
                    var heights = heightmap.Values;
                    var preds = predHeightmap.Values;
                    Debug.Assert(heights.Length == 16 * 16);

                    for (int i = 0; i < heights.Length; i++) {
                        stream.WriteShortLE(heights[i] - preds[i]);
                    }
                }
            }
        }
        private void WriteLightData(DataWriter stream)
        {
            WriteSyncTag(stream, "Lighting", 0);

            bool deltaEnc = _settings.LightEncMode == RepDataEncMode.Delta;
            var preds = new Dictionary<ChunkSection, (NibbleArray? BlockLight, NibbleArray? SkyLight)>();
            var emptyPreds = new NibbleArray(4096);

            if (deltaEnc) {
                //Temporarly overwrite the region with reproduced data.
                foreach (var section in ChunkIterator.GetSections(_region)) {
                    preds[section] = (section.BlockLight, section.SkyLight);

                    if (section.SkyLight != null) {
                        section.SkyLight = new NibbleArray(4096);
                    }
                    if (section.BlockLight != null) {
                        section.BlockLight = new NibbleArray(4096);
                    }
                }
                new Lighter().Compute(_region, _estimLightAttribs.LightAttribs);

                //Restore original data and store the computed light in the dictionary.
                foreach (var section in ChunkIterator.GetSections(_region)) {
                    var prevData = preds[section];
                    preds[section] = (section.BlockLight, section.SkyLight);
                    (section.BlockLight, section.SkyLight) = prevData;
                }
            }

            //Bitmap
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var section in chunk.Sections.ExceptNull()) {
                    int flags = 0;

                    if (section.BlockLight != null) {
                        flags |= 1;
                    }
                    if (section.SkyLight != null) {
                        flags |= 2;
                    }
                    stream.WriteByte(flags);
                }
            }

            //Payload
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var section in chunk.Sections.ExceptNull()) {
                    var (predBlockLight, predSkyLight) = deltaEnc ? preds[section] : default;

                    WriteLayer(stream, section.BlockLight, predBlockLight);
                    WriteLayer(stream, section.SkyLight, predSkyLight);
                }
            }
            static void WriteLayer(DataWriter stream, NibbleArray? vals_, NibbleArray? preds_)
            {
                if (vals_ == null) return;
                if (preds_ == null) {
                    stream.WriteBytes(vals_.Data);
                    return;
                }
                var vals = vals_.Data;
                var preds = preds_.Data;

                for (int i = 0; i < 2048; i++) {
                    byte val = vals[i];
                    byte pred = preds[i];
                    int a = (val & 15) - (pred & 15);
                    int b = (val >> 4) - (pred >> 4);
                    stream.WriteByte((a & 15) | (b & 15) << 4);
                }
            }
        }

        private void WriteSyncTag(DataWriter stream, string id, int version)
        {
            //String IDs are mostly for ~possibly~ easier debugging if something goes terribly wrong
            for (int i = 0; i < id.Length; i++) {
                stream.WriteByte(id[i]);
            }
            stream.WriteVarUInt(version);
        }
        private PartStats WritePart(DataWriter stream, string id, bool compressed, Action<DataWriter> writeContents)
        {
            stream.WriteNulString(id);
            stream.WriteIntLE(0);

            long startPos = stream.Position;
            long startTime = Stopwatch.GetTimestamp();

            if (compressed) {
                using var comp = Compressors.NewBrotliEncoder(stream, true, _settings.MetaBrotliQuality, _settings.MetaBrotliWindowSize);
                writeContents(comp);
            } else {
                writeContents(stream);
            }

            long endTime = Stopwatch.GetTimestamp();
            long timeMillis = (endTime - startTime) * 1000 / Stopwatch.Frequency;
            long endPos = stream.Position;
            int len = (int)(endPos - startPos);

            stream.Position = startPos - 4;
            stream.WriteIntLE(len);
            stream.Position = endPos;

            if (_logger.IsTraceEnabled) {
                _logger.Trace($"Stats for {id} part at '{_region}': Len={len / 1024.0:0.000}KB Time={timeMillis}ms");
            }
            return new PartStats() {
                Length = len,
                TimeMillis = (int)timeMillis
            };
        }
        
        struct PartStats
        {
            public int Length;
            public int TimeMillis;
        }
    }
}
