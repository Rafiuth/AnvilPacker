using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level.Versions.v1_16
{
    /// <summary> Handles chunks serialization for versions <c>1.16-1.16.5</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Chunk Deserialize(CompoundTag rootTag, BlockPalette palette)
        {
            var tag = rootTag.GetCompound("Level");
            
            int x = Pop<int>("xPos");
            int z = Pop<int>("zPos");
            var chunk = new Chunk(x, z, 0, 15, palette);
            chunk.DataVersion = rootTag.GetInt("DataVersion");

            foreach (CompoundTag section in Pop<ListTag>("Sections")) {
                DeserializeSection(chunk, section);
            }
            DeserializeHeightmaps(chunk.HeightMaps, Pop<CompoundTag>("Heightmaps"));
            //DeserializeScheduledTicks(chunk, Pop<ListTag>("TileTicks"));
            //DeserializeScheduledTicks(chunk, Pop<ListTag>("LiquidTicks"));
            chunk.HasLightData = tag.Remove("isLightOn");

            //Remove legacy data from upgrated worlds
            tag.Remove("HeightMap");
            chunk.Opaque = tag;

            return chunk;

            T Pop<T>(string name)
            {
                if (tag.TryGet(name, out T value)) {
                    tag.Remove(name);
                    return value;
                }
                return default;
            }
        }

        private static void DeserializeSection(Chunk chunk, CompoundTag tag)
        {
            int y = tag.GetSByte("Y");
            if (y < 0 || y >= 16) {
                return;
            }
            var blockStates = tag.GetLongArray("BlockStates", TagGetMode.Null);
            var palette = DeserializePalette(tag.GetList("Palette", TagGetMode.Null), chunk.Palette);
            
            var skyLight = tag.GetByteArray("SkyLight", TagGetMode.Null);
            var blockLight = tag.GetByteArray("BlockLight", TagGetMode.Null);

            if (blockStates == null || palette == null) {
                return;
            }
            var section = chunk.GetOrCreateSection(y);
            UnpackBlocks(blockStates, section.Blocks, palette);

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
                var name = tag.GetString("Name");
                var state = Block.Registry[name].DefaultState;

                if (tag.TryGet("Properties", out CompoundTag props)) {
                    foreach (var (k, v) in props) {
                        state = state.WithProperty(k, v.Value<string>());
                    }
                }
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
                    Type = Block.Registry[entry.GetString("i")],
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
            var blocks = PackBlocks(section.Blocks, reindexTable, palette.Count);

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
                if (state.Properties.Count > 0) {
                    var props = new CompoundTag();
                    foreach (var (name, prop) in state.Properties) {
                        props.SetString(name, prop.GetValue());
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

        private static void UnpackBlocks(long[] src, BlockId[] dst, BlockId[] palette)
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
        private static long[] PackBlocks(BlockId[] src, int[] reindexTable, int paletteLen)
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
        private static int GetPaletteBits(int size)
        {
            return Math.Max(4, Maths.CeilLog2(size));
        }
    }
}
