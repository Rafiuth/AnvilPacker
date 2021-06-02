using System;
using System.Diagnostics;
using System.IO;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
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

        public RegionDecoder(RegionBuffer region)
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
            _blockCodec.Decode(stream, CodecProgressListener.MaybeCreate(_blockCount, progress));
        }

        private void ReadHeader(DataReader stream)
        {
            _region.X = stream.ReadVarInt() * _region.Size;
            _region.Z = stream.ReadVarInt() * _region.Size;

            ReadPalette(stream);
            ReadChunkBitmap(stream);

            int blockCodecId = stream.ReadVarUInt();
            _blockCodec = BlockCodec.CreateFromId(_region, blockCodecId);
            _blockCodec.ReadSettings(stream);
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
            int count = stream.ReadVarUInt();
            var palette = new BlockPalette(count);

            for (int i = 0; i < count; i++) {
                int flags = stream.ReadByte();
                
                string name = stream.ReadNulString();
                string material = stream.ReadNulString();

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
                    (block.Attributes & ~BlockAttributes.InternalMask) == attribs &&
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
                chunk.Flags = (ChunkFlags)stream.ReadVarUInt();
                chunk.DataVersion = stream.ReadVarUInt();
                chunk.Opaque = NbtIO.Read(stream);
            }
        }
    }
}
