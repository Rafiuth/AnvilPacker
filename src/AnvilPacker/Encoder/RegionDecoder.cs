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
        private BlockCodec _blockCodec;
        private EstimatedBlockAttribs _estimAttribs = new();

        public RegionDecoder(RegionBuffer region)
        {
            _region = region;
            region.Clear();
        }

        public void Decode(DataReader stream, IProgress<double> progress = null)
        {
            int version = ReadSyncTag(stream, "main", 0);

            int headerLen = stream.ReadIntLE();
            using (var comp = Compressors.NewBrotliDecoder(stream.AsStream(headerLen), false)) {
                ReadHeader(comp);
                ReadMetadata(comp);
            }
            _blockCodec.Decode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));
        }

        private void ReadHeader(DataReader stream)
        {
            _region.X = stream.ReadVarInt() << 5;
            _region.Z = stream.ReadVarInt() << 5;

            ReadPalette(stream); //no deps
            ReadHeightmapAttribs(stream); //depends on palette
            ReadLightAttribs(stream);
            ReadChunkBitmap(stream); //no deps

            int blockCodecId = stream.ReadVarUInt();
            _blockCodec = BlockCodec.CreateFromId(_region, blockCodecId);
            _blockCodec.ReadSettings(stream);
        }

        private void ReadChunkBitmap(DataReader stream)
        {
            byte version = ReadSyncTag(stream, "cmap", 0);
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
            byte version = ReadSyncTag(stream, "plte", 0);
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

        private void ReadHeightmapAttribs(DataReader stream)
        {
            byte version = ReadSyncTag(stream, "hmap", 0);
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

            _estimAttribs.HeightmapAttribs = new EstimatedHeightmapAttribs() {
                Palette = _region.Palette,
                OpacityMap = types
            };
        }
        private void ReadLightAttribs(DataReader stream)
        {
            byte version = ReadSyncTag(stream, "lght", 0);
        }

        private void ReadMetadata(DataReader stream)
        {
            byte version = ReadSyncTag(stream, "meta", 0);
            _region.ExtraData = NbtIO.Read(stream);

            foreach (var chunk in _region.Chunks.ExceptNull()) {
                chunk.Flags = (ChunkFlags)stream.ReadVarUInt();
                chunk.DataVersion = stream.ReadVarUInt();
                chunk.Opaque = NbtIO.Read(stream);
            }
        }

        private byte ReadSyncTag(DataReader stream, string id, byte maxSupportedVersion)
        {
            for (int i = 0; i < 4; i++) {
                Ensure.That(stream.ReadByte() == id[i], "Unmatched sync tag");
            }
            byte version = stream.ReadByte();
            Ensure.That(version <= maxSupportedVersion, "Unsupported version");
            return version;
        }
    }
}
