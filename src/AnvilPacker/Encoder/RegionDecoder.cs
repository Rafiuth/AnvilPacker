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

        public RegionDecoder(RegionBuffer region)
        {
            _region = region;
            region.Clear();
        }

        public void Decode(DataReader stream, IProgress<double>? progress = null)
        {
            ReadPart(stream, "Header", true, dw => {
                ReadHeader(dw);
                ReadMetadata(dw);
            });
            ReadPart(stream, "Blocks", false, dw => {
                _blockCodec.Decode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));
            });

            if (_heightmapMode == RepDataEncMode.Strip) {
                CalcHeightmaps();
            } else {
                ReadPart(stream, "Heightmaps", true, ReadHeightmaps);
            }
            if (_lightingMode == RepDataEncMode.Strip) {
                CalcLights();
            } else {
                ReadPart(stream, "Lighting", true, ReadLightData);
            }
        }

        private void CalcHeightmaps()
        {
            var attribs = _estimHeightmapAttribs;

            foreach (var (type, isOpaque) in attribs.OpacityMap) {
                var computer = new HeightmapComputer(_region, type, isOpaque);

                foreach (var chunk in _region.ExistingChunks) {
                    if (NeedsHeightmap(chunk, type)) {
                        computer.Compute(chunk);
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
            new Lighter().Compute(_region, _estimLightAttribs.LightAttribs);
        }

        private void ReadHeader(DataReader stream)
        {
            _region.X = stream.ReadVarInt() << 5;
            _region.Z = stream.ReadVarInt() << 5;
            _heightmapMode = (RepDataEncMode)stream.ReadByte();
            _lightingMode = (RepDataEncMode)stream.ReadByte();

            //no deps
            ReadPalette(stream);
            ReadChunkBitmap(stream);

            //depends on palette & header fields above
            if (_heightmapMode != RepDataEncMode.Normal) {
                ReadHeightmapAttribs(stream);
            }
            if (_lightingMode != RepDataEncMode.Normal) {
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

            var types = new (string Name, HeightmapComputer? Computer)[numTypes];
            var predHeightmap = new Heightmap();
            bool deltaEnc = _heightmapMode == RepDataEncMode.Delta;

            for (int i = 0; i < numTypes; i++) {
                string name = stream.ReadNulString();
                HeightmapComputer? computer = null;

                if (deltaEnc) {
                    var opacityMap = _estimHeightmapAttribs.OpacityMap[name];
                    computer = new HeightmapComputer(_region, name, opacityMap);
                }
                types[i] = (name, computer);
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
                foreach (var (type, computer) in types) {
                    if (!chunk.Heightmaps.TryGetValue(type, out var heightmap)) continue;

                    if (deltaEnc) {
                        computer!.Compute(chunk, predHeightmap);
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

            if (_lightingMode == RepDataEncMode.Delta) {
                throw new NotImplementedException("Delta encoded lighting not implemented.");
            }
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

            //Payload
            foreach (var chunk in _region.ExistingChunks) {
                foreach (var section in chunk.Sections.ExceptNull()) {
                    if (section.BlockLight != null) {
                        stream.ReadBytes(section.BlockLight.Data);;
                    }
                    if (section.SkyLight != null) {
                        stream.ReadBytes(section.SkyLight.Data);
                    }
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

            if (compressed) {
                using var comp = Compressors.NewBrotliDecoder(stream.AsStream(length), true);
                readContents(comp);
            } else {
                //TODO: limit by length
                readContents(stream);
            }
        }
    }
}
