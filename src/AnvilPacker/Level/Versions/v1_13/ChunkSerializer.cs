using System;
using System.Collections.Generic;
using System.Diagnostics;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level.Versions.v1_13
{
    /// <summary> Handles chunks serialization for versions <c>1.13-1.16.5</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        public Chunk Deserialize(CompoundTag rootTag, BlockPalette palette)
        {
            var tag = rootTag.GetCompound("Level");
            
            int x = tag.Pop<int>("xPos");
            int z = tag.Pop<int>("zPos");
            var version = (DataVersion)rootTag.Pop<int>("DataVersion");
            var chunk = new Chunk(x, z, palette);
            chunk.Opaque = rootTag;
            chunk.DataVersion = version;

            if (tag.TryGet("Sections", out ListTag? sectList)) {
                for (int i = 0; i < sectList.Count; i++) {
                    var sectTag = sectList.Get<CompoundTag>(i);
                    DeserializeSection(chunk, sectTag);
                    if (sectTag.Count <= 1) { //is "Y" the only value left?
                        sectList.RemoveAt(i--);
                    }
                }
                if (sectList.Count == 0) {
                    tag.Remove("Sections");
                }
            }
            DeserializeHeightmaps(chunk, tag.PopMaybe<CompoundTag>("Heightmaps"));
            chunk.SetFlag(ChunkFlags.LightDirty, version >= DataVersion.ForcedLightRecalc && !tag.PopMaybe<bool>("isLightOn"));

            return chunk;
        }

        private static void DeserializeSection(Chunk chunk, CompoundTag tag)
        {
            int y = tag.GetSByte("Y");
 
            var blockStates = tag.PopMaybe<long[]>("BlockStates");
            var palette = DeserializePalette(tag.PopMaybe<ListTag>("Palette"), chunk);
            
            var skyLight = tag.PopMaybe<byte[]>("SkyLight");
            var blockLight = tag.PopMaybe<byte[]>("BlockLight");

            if (blockStates == null || palette == null) {
                return;
            }
            var section = chunk.GetOrCreateSection(y);
            UnpackBlocks(blockStates, section, palette);

            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }
        }
        private static BlockId[]? DeserializePalette(ListTag? list, Chunk chunk)
        {
            if (list == null) return null;

            var palette = new BlockId[list.Count];
            var destPalette = chunk.Palette;
            int i = 0;
            foreach (CompoundTag tag in list) {
                var state = BlockRegistry.ParseState(tag, chunk.DataVersion);
                palette[i++] = destPalette.GetOrAddId(state);
            }
            if (palette.Length == 1 && destPalette.GetState(palette[0]).Material == BlockMaterial.Air) {
                return null;
            }
            return palette;
        }
        private void DeserializeHeightmaps(Chunk chunk, CompoundTag? tag)
        {
            if (tag == null) return;

            foreach (var (type, rawBits) in tag) {
                var storage = CreateStorage(chunk, 256, 9, rawBits.Value<long[]>());

                var heightmap = new Heightmap();
                storage.Unpack(heightmap.Values);
                chunk.Heightmaps.Add(type, heightmap);
            }
        }


        public CompoundTag Serialize(Chunk chunk)
        {
            var tag = new CompoundTag();
            tag.SetInt("xPos", chunk.X);
            tag.SetInt("zPos", chunk.Z);

            var sections = new ListTag();
            var reindexTable = new BlockId[chunk.Palette.Count];

            foreach (var section in chunk.Sections.ExceptNull()) {
                Debug.Assert(section.Palette == chunk.Palette);
                sections.Add(SerializeSection(section, reindexTable));
            }
            tag.SetList("Sections", sections);
            tag.SetCompound("Heightmaps", SerializeHeightmaps(chunk));

            bool lightDirty = chunk.HasFlag(ChunkFlags.LightDirty);
            if (chunk.DataVersion >= DataVersion.ForcedLightRecalc) {
                tag.SetBool("isLightOn", !lightDirty);
            } else if (lightDirty) {
                throw new NotSupportedException($"Light can't be maked as dirty in this chunk version (v1.13+{chunk.DataVersion})");
            }

            var rootTag = new CompoundTag();
            rootTag.SetCompound("Level", tag);
            rootTag.SetInt("DataVersion", (int)chunk.DataVersion);
            ChunkNbtUtils.MergeOpaque(chunk.Opaque, rootTag);

            return rootTag;
        }

        private CompoundTag SerializeSection(ChunkSection section, BlockId[] reindexTable)
        {
            var tag = new CompoundTag();
            var palette = SerializePalette(section, reindexTable);
            var blocks = PackBlocks(section, reindexTable, palette.Count);

            tag.SetSByte("Y", (sbyte)section.Y);
            tag.SetList("Palette", palette);
            tag.SetLongArray("BlockStates", blocks);

            if (section.SkyLight != null) {
                tag.SetByteArray("SkyLight", section.SkyLight.Data);
            }
            if (section.BlockLight != null) {
                tag.SetByteArray("BlockLight", section.BlockLight.Data);
            }
            return tag;
        }
        private ListTag SerializePalette(ChunkSection section, BlockId[] indices)
        {
            var palette = section.Palette;
            indices.Fill(BlockId.Invalid);

            var list = new ListTag();
            foreach (var id in section.Blocks) {
                if (indices[id] != BlockId.Invalid) continue;

                var state = palette.GetState(id);
                var tag = new CompoundTag();
                tag.SetString("Name", state.Block.Name.ToString());
                if (state.Properties.Length > 0) {
                    var props = new CompoundTag();
                    foreach (var (name, value) in state.Properties) {
                        props.SetString(name, value);
                    }
                    tag.SetCompound("Properties", props);
                }
                indices[id] = (ushort)list.Count;
                list.Add(tag);
            }
            return list;
        }
        private CompoundTag SerializeHeightmaps(Chunk chunk)
        {
            var tag = new CompoundTag();
            foreach (var (type, heights) in chunk.Heightmaps) {
                var storage = CreateStorage(chunk, 256, 9);
                storage.Pack(heights.Values);
                tag.SetLongArray(type, storage.Data);
            }
            return tag;
        }

        private static void UnpackBlocks(long[] src, ChunkSection section, BlockId[] palette)
        {
            int elemBits = GetPaletteBits(palette.Length);
            var storage = CreateStorage(section.Chunk, 4096, elemBits, src);
            storage.Unpack(new BlockStorageVisitor() {
                Blocks = section.Blocks,
                Palette = palette
            });
        }
        private static long[] PackBlocks(ChunkSection section, BlockId[] palette, int paletteLen)
        {
            int elemBits = GetPaletteBits(paletteLen);
            var storage = CreateStorage(section.Chunk, 4096, elemBits);
            storage.Pack(new BlockStorageVisitor() {
                Blocks = section.Blocks,
                Palette = palette
            });
            return storage.Data;
        }
        
        private static IBitStorage CreateStorage(Chunk chunk, int count, int bits, long[]? data = null)
        {
            if (chunk.DataVersion >= DataVersion.v1_16_s13) {
                return new SparseBitStorage(count, bits, data);
            } else {
                return new PackedBitStorage(count, bits, data);
            }
        }
        private static int GetPaletteBits(int size)
        {
            return Math.Max(4, Maths.CeilLog2(size));
        }

        private struct BlockStorageVisitor : IBitStorageVisitor
        {
            public BlockId[] Blocks;
            public BlockId[] Palette;

            public void Use(int index, int value) => Blocks[index] = Palette[value];
            public int Create(int index) => Palette[Blocks[index]];
        }
    }
}
