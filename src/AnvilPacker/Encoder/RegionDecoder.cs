#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder
{
    public class RegionDecoder
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private RegionBuffer _region;
        private int _blockCount; //updated by ReadChunkBitmap()
        private BlockCodec _blockCodec = null!;
        private EstimatedHeightmapAttribs _estimHeightmapAttribs = null!;
        private EstimatedLightAttribs _estimLightAttribs = null!;
        private RepDataEncMode _heightmapMode, _lightingMode;

        private RegionDecoderSettings _settings;

        public RegionDecoder(RegionBuffer region, RegionDecoderSettings settings)
        {
            _region = region;
            _settings = settings;
            region.Clear();
        }

        public void Decode(DataReader stream, IProgress<double>? progress = null)
        {
            int version = ReadSyncTag(stream, "Root", 0);

            ReadPart(stream, "Header", true, dw => {
                ReadHeader(dw);
                ReadMetadata(dw);
            });
            ReadPart(stream, "Blocks", false, dw => {
                _blockCodec.Decode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));
            });

            if (_heightmapMode != RepDataEncMode.Strip) {
                ReadPart(stream, "Heightmaps", true, ReadHeightmaps);
            }
            if (_lightingMode != RepDataEncMode.Strip) {
                ReadPart(stream, "Lighting", true, ReadLightData);
            } else {
                ReadPart(stream, "LightBorders", true, ReadLightBorders);
            }
            Prime();
        }

        private void Prime()
        {
            long startTime = Stopwatch.GetTimestamp();

            if (_heightmapMode == RepDataEncMode.Strip) {
                CalcHeightmaps();
            }
            if (_lightingMode == RepDataEncMode.Strip) {
                CalcLights();
            }

            long endTime = Stopwatch.GetTimestamp();
            _logger.Debug("Region primed in {0}ms", (endTime - startTime) * 1000 / Stopwatch.Frequency);
        }
        private void CalcHeightmaps()
        {
            var attribs = _estimHeightmapAttribs;

            foreach (var (type, isBlockOpaque) in attribs.OpacityMap) {
                foreach (var chunk in _region.ExistingChunks) {
                    if (NeedsHeightmap(chunk, type)) {
                        var heightmap = chunk.GetOrCreateHeightmap(type);
                        heightmap.Compute(chunk, isBlockOpaque);
                    }
                }
            }
            bool NeedsHeightmap(Chunk chunk, string type)
            {
                if (DataVersions.IsBeforeFlattening(chunk.DataVersion)) {
                    return type == Heightmap.TYPE_LEGACY;
                }
                var status = chunk.Opaque?["Level"]?["Status"]?.Value<string>();
                bool statusComplete = status is "full" or "heightmaps" or "spawn" or "light";
                return statusComplete && !type.EndsWith("_WG");
            }
        }
        private void CalcLights()
        {
            bool skipLighting = false;

            if (_settings.DontLit) {
                skipLighting = true;

                foreach (var chunk in _region.ExistingChunks) {
                    if (chunk.DataVersion >= DataVersions.v1_14_2_pre4) {
                        chunk.SetFlag(ChunkFlags.LightDirty);
                    } else {
                        skipLighting = false;
                    }
                }
            }
            if (!skipLighting) {
                new Lighter(_region, _estimLightAttribs.LightAttribs, true).Compute();
            }
        }

        private void ReadHeader(DataReader stream)
        {
            int version = ReadSyncTag(stream, "Header", 0);

            _region.X = stream.ReadVarInt() << 5;
            _region.Z = stream.ReadVarInt() << 5;
            _heightmapMode = (RepDataEncMode)stream.ReadByte();
            _lightingMode = (RepDataEncMode)stream.ReadByte();

            //no deps
            ReadPalette(stream);
            ReadChunkBitmap(stream);

            //depends on palette & header fields above
            if (_heightmapMode != RepDataEncMode.Keep) {
                ReadHeightmapAttribs(stream);
            }
            if (_lightingMode != RepDataEncMode.Keep) {
                ReadLightAttribs(stream);
            }
            int blockCodecId = stream.ReadVarUInt();
            _blockCodec = BlockCodec.CreateFromId(_region, blockCodecId);
            _blockCodec.ReadHeader(stream);
        }

        private void ReadChunkBitmap(DataReader stream)
        {
            int version = ReadSyncTag(stream, "ChunkBMP", 0);
            int minY = stream.ReadVarInt();
            int maxY = stream.ReadVarInt();

            //chunk bitmap
            for (int z = 0; z < _region.Size; z++) {
                for (int x = 0; x < _region.Size; x++) {
                    if (stream.ReadByte() != 0) {
                        var chunk = new Chunk(_region.X + x, _region.Z + z, _region.Palette, minY, maxY);
                        _region.PutChunk(chunk);
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
            int version = ReadSyncTag(stream, "Palette", 0);
            int count = stream.ReadVarUInt();
            var palette = new BlockPalette(count);

            for (int i = 0; i < count; i++) {
                var flags = (BlockFlags)stream.ReadVarUInt();

                BlockState block;

                if (flags.HasFlag(BlockFlags.Legacy)) {
                    int id = stream.ReadVarUInt();
                    block = BlockRegistry.GetLegacyState(id);
                } else {
                    string name = stream.ReadNulString();
                    block = BlockRegistry.ParseState(name);
                }
                palette.Add(block);
            }
            _region.Palette = palette;
        }

        private void ReadMetadata(DataReader stream)
        {
            int version = ReadSyncTag(stream, "Meta", 0);
            _region.ExtraData = NbtIO.Read(stream);

            foreach (var chunk in _region.ExistingChunks) {
                chunk.Flags = (ChunkFlags)stream.ReadVarUInt();
                chunk.DataVersion = stream.ReadVarUInt();
                chunk.Opaque = NbtIO.Read(stream);
            }
        }

        private void ReadHeightmapAttribs(DataReader stream)
        {
            int version = ReadSyncTag(stream, "HeightAttribs", 0);
            int numTypes = stream.ReadVarUInt();

            int numBlocks = _region.Palette.Count;
            var types = new Dictionary<string, bool[]>();

            for (int i = 0; i < numTypes; i++) {
                string type = stream.ReadNulString();
                var isOpaque = new bool[numBlocks];
                for (int j = 0; j < numBlocks; j++) {
                    isOpaque[j] = stream.ReadBool();
                }
                types.Add(type, isOpaque);
            }
            _estimHeightmapAttribs = new EstimatedHeightmapAttribs() {
                Palette = _region.Palette,
                OpacityMap = types
            };
        }
        private void ReadLightAttribs(DataReader stream)
        {
            int version = ReadSyncTag(stream, "LightAttribs", 0);

            var blockAttribs = new BlockLightInfo[_region.Palette.Count];
            for (int i = 0; i < blockAttribs.Length; i++) {
                blockAttribs[i] = new BlockLightInfo(stream.ReadByte());
            }

            _estimLightAttribs = new EstimatedLightAttribs() {
                Palette = _region.Palette,
                LightAttribs = blockAttribs
            };
        }

        private void ReadHeightmaps(DataReader stream)
        {
            int version = ReadSyncTag(stream, "Heightmaps", 0);
        
            int numTypes = stream.ReadVarUInt();

            var types = new (string Name, bool[]? IsBlockOpaque)[numTypes];
            var predHeightmap = new Heightmap();
            bool deltaEnc = _heightmapMode == RepDataEncMode.Delta;

            for (int i = 0; i < numTypes; i++) {
                string name = stream.ReadNulString();
                var isBlockOpaque = deltaEnc ? _estimHeightmapAttribs.OpacityMap[name] : null;
                
                types[i] = (name, isBlockOpaque);
            }

            //Bitmap
            foreach (var chunk in _region.ExistingChunks) {
                int minY = chunk.MinSectionY * 16;
                int mask = stream.ReadVarUInt();

                for (int i = 0; i < types.Length; i++) {
                    if ((mask & (1 << i)) != 0) {
                        chunk.Heightmaps.Add(types[i].Name, new Heightmap(minY));
                    }
                }
            }

            //Payload
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var (type, isBlockOpaque) in types) {
                    if (!chunk.Heightmaps.TryGetValue(type, out var heightmap)) continue;

                    if (deltaEnc) {
                        predHeightmap.Compute(chunk, isBlockOpaque!);
                    }
                    var heights = heightmap.Values;
                    var preds = predHeightmap.Values;
                    Debug.Assert(heights.Length == 16 * 16);

                    for (int i = 0; i < heights.Length; i++) {
                        heights[i] = (short)(stream.ReadShortLE() + preds[i]);
                    }
                }
            }
        }
        private void ReadLightData(DataReader stream)
        {
            int version = ReadSyncTag(stream, "Lighting", 0);

            //Bitmap
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var section in chunk.Sections.ExceptNull()) {
                    int flags = stream.ReadByte();

                    if ((flags & 1) != 0) {
                        section.BlockLight = new NibbleArray(4096);
                    }
                    if ((flags & 2) != 0) {
                        section.SkyLight = new NibbleArray(4096);
                    }
                }
            }

            bool deltaEnc = _lightingMode == RepDataEncMode.Delta;
            var preds = new Dictionary<ChunkSection, (NibbleArray? BlockLight, NibbleArray? SkyLight)>();

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
                new Lighter(_region, _estimLightAttribs.LightAttribs).Compute();

                //Restore original data and store the computed light in the dictionary.
                foreach (var section in ChunkIterator.GetSections(_region)) {
                    var prevData = preds[section];
                    preds[section] = (section.BlockLight, section.SkyLight);
                    (section.BlockLight, section.SkyLight) = prevData;
                }
            }

            //Payload
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var section in chunk.Sections.ExceptNull()) {
                    var (predBlockLight, predSkyLight) = deltaEnc ? preds[section] : default;

                    ReadLayer(stream, section.BlockLight, predBlockLight);
                    ReadLayer(stream, section.SkyLight, predSkyLight);
                }
            }
            static void ReadLayer(DataReader stream, NibbleArray? vals_, NibbleArray? preds_)
            {
                if (vals_ == null) return;
                if (preds_ == null) {
                    stream.ReadBytes(vals_.Data);
                    return;
                }
                var vals = vals_.Data;
                var preds = preds_.Data;

                for (int i = 0; i < 2048; i++) {
                    byte delta = stream.ReadByte();
                    byte pred = preds[i];
                    int a = (delta & 15) + (pred & 15);
                    int b = (delta >> 4) + (pred >> 4);
                    vals[i] = (byte)((a & 15) | (b & 15) << 4);
                }
            }
        }
        private void ReadLightBorders(DataReader stream)
        {
            int version = ReadSyncTag(stream, "LightBorders", 0);

            var (minSy, maxSy) = _region.GetChunkYExtents();

            foreach (var layer in new[] { LightLayer.Sky, LightLayer.Block }) {
                ReadPlane(true,  false, layer); //X-
                ReadPlane(true,  true,  layer); //X+
                ReadPlane(false, false, layer); //Z-
                ReadPlane(false, true,  layer); //Z+
            }

            void ReadPlane(bool axisX, bool axisP, LightLayer layer)
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

                        var data = section.GetOrCreateLightData(layer).Data;

                        if (axisX) {
                            ReadPlaneX(stream, data, axisP);
                        } else {
                            ReadPlaneZ(stream, data, axisP);
                        }
                    }
                }
            }
            static void ReadPlaneX(DataReader stream, byte[] data, bool plus)
            {
                for (int by = 0; by < 16; by++) {
                    int ofs = ChunkSection.GetIndex(plus ? 15 : 0, by, 0);

                    for (int bz = 0; bz < 16; bz += 2) {
                        int v = stream.ReadByte();
                        NibbleArray.Set(data, ofs + (bz + 0) * 16, v & 15);
                        NibbleArray.Set(data, ofs + (bz + 1) * 16, v >> 4);
                    }
                }
            }
            static void ReadPlaneZ(DataReader stream, byte[] data, bool plus)
            {
                for (int by = 0; by < 16; by++) {
                    int ofs = ChunkSection.GetIndex(0, by, plus ? 15 : 0) >> 1;
                    stream.ReadBytes(data, ofs, 8);
                }
            }
        }

        private int ReadSyncTag(DataReader stream, string id, int maxSupportedVersion)
        {
            for (int i = 0; i < id.Length; i++) {
                Ensure.That(stream.ReadByte() == id[i], "Unmatched sync tag");
            }
            int version = stream.ReadVarUInt();
            Ensure.That(version <= maxSupportedVersion, "Unsupported version");
            return version;
        }
        private void ReadPart(DataReader stream, string id, bool compressed, Action<DataReader> readContents)
        {
            string dataId = stream.ReadNulString();
            int length = stream.ReadIntLE();

            if (dataId != id) {
                throw new InvalidOperationException($"Unmatched part id: expecting '{id}', got '{dataId}'");
            }
            long startTime = Stopwatch.GetTimestamp();

            if (compressed) {
                using var comp = Compressors.NewBrotliDecoder(stream.AsStream(length), true);
                readContents(comp);
            } else {
                //TODO: limit by length
                readContents(stream);
            }
            long endTime = Stopwatch.GetTimestamp();
            long timeMillis = (endTime - startTime) * 1000 / Stopwatch.Frequency;
            if (_logger.IsDebugEnabled) {
                _logger.Debug($"Decoded part {id} at '{_region}'. Len={length / 1024.0:0.000}KB Time={timeMillis}ms");
            }
        }
    }
}
