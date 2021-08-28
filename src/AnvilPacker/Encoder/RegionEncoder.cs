using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;
using AnvilPacker.Level.Physics;

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
        private RepDataEncMode _heightmapMode, _lightingMode;

        public RegionEncoder(RegionBuffer region, RegionEncoderSettings settings)
        {
            _region = region;
            _settings = settings;

            _blockCodec = settings.BlockCodec.Create(region);

            _heightmapMode = settings.HeightmapEncMode;
            _lightingMode = settings.LightEncMode;
            Ensure.That(_lightingMode != RepDataEncMode.Delta, "Light data cannot be delta encoded.");
            UpdateAutoRepDataMode();
        }
        private void UpdateAutoRepDataMode()
        {
            bool allBlocksKnown = _region.Palette.All(b => b.Block.IsKnown);
            var mode = allBlocksKnown ? RepDataEncMode.Strip : RepDataEncMode.Keep;

            if (_heightmapMode == RepDataEncMode.Auto) {
                _heightmapMode = mode;
            }
            if (_lightingMode == RepDataEncMode.Auto) {
                _lightingMode = mode;
            }
        }

        public void Encode(DataWriter stream, IProgress<double>? progress = null)
        {
            WriteSyncTag(stream, "Root", 0);

            var headerStats = WritePart(stream, "Header", true, dw => {
                WriteHeader(dw);
                WriteMetadata(dw);
            });
            var blockDataStats = WritePart(stream, "Blocks", false, dw => {
                _blockCodec.Encode(dw, CodecProgressListener.MaybeCreate(_blockCount, progress));
            });

            if (_heightmapMode != RepDataEncMode.Strip) {
                WritePart(stream, "Heightmaps", true, WriteHeightmaps);
            }
            if (_lightingMode != RepDataEncMode.Strip) {
                WritePart(stream, "Lighting", true, WriteLightData);
            } else {
                WritePart(stream, "LightBorders", true, WriteLightBorders);
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
            WriteSyncTag(stream, "Header", 0);

            //Note: arithmetic shift gives different results from div when the dividend is negative.
            //regionX = floorDiv(chunkX / 32) = x >> 5
            stream.WriteVarInt(_region.X >> 5);
            stream.WriteVarInt(_region.Z >> 5);
            stream.WriteByte((byte)_heightmapMode);
            stream.WriteByte((byte)_lightingMode);

            //no deps
            WritePalette(stream);
            WriteChunkBitmap(stream);

            //depends on palette & header fields above
            if (_heightmapMode != RepDataEncMode.Keep) {
                WriteHeightmapAttribs(stream);
            }
            if (_lightingMode != RepDataEncMode.Keep) {
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
                bool isLegacy = state.HasAttrib(BlockAttributes.Legacy);

                stream.WriteVarUInt(
                    isLegacy ? 0x01 : 0
                );

                if (isLegacy) {
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
                stream.WriteVarUInt((int)chunk.DataVersion);
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
            //TODO: use v1 only if necessary
            WriteSyncTag(stream, "LightAttribs", 1);

            var attribs = _estimLightAttribs;
            attribs.Estimate(_region);

            int paletteLen = _region.Palette.Count;
            var blockAttribs = attribs.LightAttribs;
            var blockShapes = attribs.OcclusionShapes;
            var shapeCache = new Dictionary<VoxelShape, int>();

            Ensure.That(blockAttribs.Length == paletteLen && blockShapes.Length == paletteLen);

            for (int i = 0; i < paletteLen; i++) {
                var attrib = blockAttribs[i];
                stream.WriteByte(attrib.Emission << 4 | attrib.Opacity);
                stream.WriteByte(attrib.UseShapeForOcclusion ? 0x01 : 0);

                if (attrib.UseShapeForOcclusion) {
                    WriteShape(i);
                }
            }

            void WriteShape(int currId)
            {
                var shape = blockShapes[currId];
                if (shapeCache.TryGetValue(shape, out int id)) {
                    stream.WriteVarUInt(id + 1);
                } else {
                    shapeCache.Add(shape, currId);

                    stream.WriteVarUInt(0);
                    stream.WriteVarUInt(shape.Boxes.Length);
                    foreach (var box in shape.Boxes) {
                        stream.WriteBytes(box.UnsafeDataSpan);
                    }
                }
            }
        }

        private void WriteHeightmaps(DataWriter stream)
        {
            WriteSyncTag(stream, "Heightmaps", 0);

            var types = new Dictionary<string, (int Mask, bool[]? IsBlockOpaque)>();
            var predHeightmap = new Heightmap();
            bool deltaEnc = _heightmapMode == RepDataEncMode.Delta;

            //Find existing heightmap types
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var (type, heightmap) in chunk.Heightmaps) {
                    if (types.ContainsKey(type)) continue;
                    Ensure.That(types.Count <= 32, "Can't have more than 32 heightmap types.");

                    var isBlockOpaque = deltaEnc ? _estimHeightmapAttribs.OpacityMap[type] : null;
                    
                    types[type] = (1 << types.Count, isBlockOpaque);
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
                        predHeightmap.Compute(chunk, data.IsBlockOpaque!);
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
                    if (section.BlockLight != null) {
                        stream.WriteBytes(section.BlockLight.Data);
                    }
                    if (section.SkyLight != null) {
                        stream.WriteBytes(section.SkyLight.Data);
                    }
                }
            }
        }
        private void WriteLightBorders(DataWriter stream)
        {
            WriteSyncTag(stream, "LightBorders", 0);

            var (minSy, maxSy) = _region.GetChunkYExtents();
            var emptyPlaneData = new byte[16 * 8];

            foreach (var layer in new[] { LightLayer.Sky, LightLayer.Block }) {
                WritePlane(true,  false, layer); //X-
                WritePlane(true,  true,  layer); //X+
                WritePlane(false, false, layer); //Z-
                WritePlane(false, true,  layer); //Z+
            }

            void WritePlane(bool axisX, bool axisP, LightLayer layer)
            {
                //P[x, y] =
                //  D[ 0, y,  x] for X-
                //  D[15, y,  x] for X+
                //  D[x,  y,  0] for Z-
                //  D[x,  y, 15] for Z+
                for (int cy = minSy; cy <= maxSy; cy++) {
                    for (int ch = 0; ch < 32; ch++) {
                        var section = _region.GetSection(
                            !axisX ? ch : (axisP ? 31 : 0),
                            cy,
                            axisX ? ch : (axisP ? 31 : 0)
                        );
                        if (section == null) continue;

                        var data = section.GetLightData(layer)?.Data;
                        if (data == null) {
                            stream.WriteBytes(emptyPlaneData);
                            continue;
                        }
                        if (axisX) {
                            WritePlaneX(stream, data, axisP);
                        } else {
                            WritePlaneZ(stream, data, axisP);
                        }
                    }
                }
            }
            static void WritePlaneX(DataWriter stream, byte[] data, bool plus)
            {
                for (int by = 0; by < 16; by++) {
                    int ofs = ChunkSection.GetIndex(plus ? 15 : 0, by, 0);

                    for (int bz = 0; bz < 16; bz += 2) {
                        int a = NibbleArray.Get(data, ofs + (bz + 0) * 16);
                        int b = NibbleArray.Get(data, ofs + (bz + 1) * 16);
                        stream.WriteByte(a | b << 4);
                    }
                }
            }
            static void WritePlaneZ(DataWriter stream, byte[] data, bool plus)
            {
                for (int by = 0; by < 16; by++) {
                    int ofs = ChunkSection.GetIndex(0, by, plus ? 15 : 0) >> 1;
                    stream.WriteBytes(data, ofs, 8);
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

            if (_logger.IsDebugEnabled) {
                _logger.Debug($"Stats for {id} part at '{_region}': Len={len / 1024.0:0.000}KB Time={timeMillis}ms");
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
