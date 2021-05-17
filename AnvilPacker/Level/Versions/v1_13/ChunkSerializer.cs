using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level.Versions.v1_13
{
    /// <summary> Handles chunks serialization for versions <c>1.16-1.16.5</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Chunk Deserialize(CompoundTag rootTag, BlockPalette palette)
        {
            var tag = rootTag.GetCompound("Level");
            
            int x = tag.Pop<int>("xPos");
            int z = tag.Pop<int>("zPos");
            var chunk = new Chunk(x, z, 0, 15, palette);
            chunk.DataVersion = rootTag.GetInt("DataVersion");

            foreach (CompoundTag section in tag.MaybePop<ListTag>("Sections")) {
                DeserializeSection(chunk, section);
            }
            DeserializeHeightmaps(chunk.HeightMaps, tag.MaybePop<CompoundTag>("Heightmaps"));
            //DeserializeScheduledTicks(chunk, Pop<ListTag>("TileTicks"));
            //DeserializeScheduledTicks(chunk, Pop<ListTag>("LiquidTicks"));
            chunk.HasLightData = tag.MaybePop<bool>("isLightOn");

            //Remove legacy data from upgrated worlds
            tag.Remove("HeightMap");
            chunk.Opaque = tag;

            return chunk;
        }

        private static void DeserializeSection(Chunk chunk, CompoundTag tag)
        {
            int y = tag.Pop<sbyte>("Y");
            if (y < 0 || y >= 16) {
                return;
            }
            var blockStates = tag.MaybePop<long[]>("BlockStates");
            var palette = DeserializePalette(tag.MaybePop<ListTag>("Palette"), chunk.Palette);
            
            var skyLight = tag.MaybePop<byte[]>("SkyLight");
            var blockLight = tag.MaybePop<byte[]>("BlockLight");

            if (blockStates == null || palette == null) {
                return;
            }
            var section = chunk.GetOrCreateSection(y);
            UnpackBlocks(blockStates, section.Blocks, palette, chunk.DataVersion >= 2529);

            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }
        }
        private static BlockId[] DeserializePalette(ListTag list, BlockPalette destPalette)
        {
            if (list == null) return null;

            var palette = new BlockId[list.Count];
            int i = 0;
            foreach (CompoundTag tag in list) {
                var state = BlockRegistry.ParseState(tag);
                palette[i++] = destPalette.GetOrAddId(state);
            }
            if (palette.Length == 1 && destPalette.GetState(palette[0]).Material == BlockMaterial.Air) {
                return null;
            }
            return palette;
        }
        private void DeserializeHeightmaps(HeightMaps maps, CompoundTag tag)
        {
            if (tag == null) return;

            foreach (var (typeName, rawBits) in tag) {
                var packedHeights = new SparseBitStorage(256, 9, rawBits.Value<long[]>());

                var type = HeightMapType.ForName(typeName);
                var heights = maps.Get(type, true);
                for (int i = 0; i < 256; i++) {
                    heights[i] = (short)packedHeights[i];
                }
            }
        }
        private static void DeserializeScheduledTicks(Chunk chunk, ListTag tag)
        {
            if (tag == null) return;

            var list = chunk.ScheduledTicks;
            int xo = chunk.X * 16;
            int zo = chunk.Z * 16;
            foreach (CompoundTag entry in tag) {
                var st = new ScheduledTick() {
                    Type = BlockRegistry.GetBlock(entry.GetString("i")),
                    X = (sbyte)(entry.GetInt("x") - xo),
                    Z = (sbyte)(entry.GetInt("z") - zo),
                    Y = (short)entry.GetInt("y"),
                    Delay = entry.GetInt("t"),
                    Priority = entry.GetInt("p")
                };
                list.Add(st);
            }
        }

        public CompoundTag Serialize(Chunk chunk)
        {
            var tag = new CompoundTag();
            tag.SetInt("xPos", chunk.X);
            tag.SetInt("zPos", chunk.Z);

            var sections = new ListTag();
            var reindexTable = new int[chunk.Palette.Count];

            foreach (var section in chunk.Sections.ExceptNull()) {
                sections.Add(SerializeSection(section, reindexTable));
            }
            tag.SetList("Sections", sections);
            tag.SetCompound("Heightmaps", SerializeHeightmaps(chunk.HeightMaps));
            tag.SetBool("isLightOn", chunk.HasLightData);

            CopyOpaque(tag, chunk.Opaque);

            var rootTag = new CompoundTag();
            rootTag.SetCompound("Level", tag);
            rootTag.SetInt("DataVersion", chunk.DataVersion);
            return rootTag;
        }

        private CompoundTag SerializeSection(ChunkSection section, int[] reindexTable)
        {
            var tag = new CompoundTag();
            var palette = SerializePalette(section, reindexTable);
            var blocks = PackBlocks(section.Blocks, reindexTable, palette.Count, section.Chunk.DataVersion >= 2529);

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
        private ListTag SerializePalette(ChunkSection section, int[] reindexTable)
        {
            var palette = section.Palette;
            reindexTable.Fill(-1);

            var list = new ListTag();
            foreach (var id in section.Blocks) {
                if (reindexTable[id] >= 0) continue;

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
                reindexTable[id] = list.Count;
                list.Add(tag);
            }
            return list;
        }
        private CompoundTag SerializeHeightmaps(HeightMaps maps)
        {
            var tag = new CompoundTag();
            foreach (var (type, heights) in maps) {
                var packedHeights = new SparseBitStorage(256, 9);

                for (int i = 0; i < 256; i++) {
                    packedHeights[i] = (short)heights[i];
                }
                tag.SetLongArray(type.Name, packedHeights.Data);
            }
            return tag;
        }

        private void CopyOpaque(CompoundTag tag, CompoundTag opaque)
        {
            foreach (var (k, v) in opaque) {
                if (tag.ContainsKey(k)) {
                    //TODO: recursive merging?
                    _logger.Warn($"Opaque tag '{k}' already exists in serialized chunk, keeping existing one.");
                    continue;
                }
                tag.Set(k, v);
            }
        }

        private static void UnpackBlocks(long[] src, BlockId[] dst, BlockId[] palette, bool sparse)
        {
            if (sparse) {
                UnpackSparse(src, dst, palette);
            } else {
                Unpack(src, dst, palette);
            }
            static void UnpackSparse(long[] src, BlockId[] dst, BlockId[] palette)
            {
                int elemBits = GetPaletteBits(palette.Length);
                int valsPerLong = 64 / elemBits;
                int mask = (1 << elemBits) - 1;
                int srcPos = 0;

                for (int i = 0; i < dst.Length; i += valsPerLong) {
                    int valCount = Math.Min(dst.Length - i, valsPerLong);
                    long vals = src[srcPos++];

                    for (int j = 0; j < valCount; j++) {
                        dst[i + j] = palette[vals & mask];
                        vals >>= elemBits;
                    }
                }
            }
            static void Unpack(long[] src, BlockId[] dst, BlockId[] palette)
            {
                int elemBits = GetPaletteBits(palette.Length);
                var storage = new PackedBitStorage(dst.Length, elemBits, src);
                for (int i = 0; i < dst.Length; i++) {
                    dst[i] = palette[storage[i]];
                }
            }
        }
        private static long[] PackBlocks(BlockId[] src, int[] reindexTable, int paletteLen, bool sparse)
        {
            if (sparse) {
                return PackSparse(src, reindexTable, paletteLen);
            } else {
                return Pack(src, reindexTable, paletteLen);
            }
            static long[] PackSparse(BlockId[] src, int[] reindexTable, int paletteLen)
            {
                int elemBits = GetPaletteBits(paletteLen);
                int valsPerLong = 64 / elemBits;
                var dst = new long[Maths.CeilDiv(src.Length, valsPerLong)];
                int dstPos = 0;

                for (int i = 0; i < src.Length; i += valsPerLong) {
                    int valCount = Math.Min(src.Length - i, valsPerLong);
                    long vals = 0;

                    for (int j = 0; j < valCount; j++) {
                        vals |= (long)reindexTable[src[i + j]] << (j * elemBits);
                    }
                    dst[dstPos++] = vals;
                }
                return dst;
            }
            static long[] Pack(BlockId[] src, int[] reindexTable, int paletteLen)
            {
                int elemBits = GetPaletteBits(paletteLen);
                var storage = new PackedBitStorage(src.Length, elemBits);
                for (int i = 0; i < src.Length; i++) {
                    storage[i] = reindexTable[src[i]];
                }
                return storage.Data;
            }
        }
        
        private static int GetPaletteBits(int size)
        {
            return Math.Max(4, Maths.CeilLog2(size));
        }
    }
}
