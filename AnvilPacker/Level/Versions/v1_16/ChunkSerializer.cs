using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Versions.v1_16
{
    /// <summary> Handles chunks serialization for versions <c>1.16-1.16.5</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        public Chunk Deserialize(CompoundTag tag, BlockPalette palette)
        {
            int x = tag.GetInt("xPos");
            int z = tag.GetInt("zPos");
            var chunk = new Chunk(x, z, palette);

            foreach (CompoundTag section in Pop<ListTag>("Sections")) {
                DeserializeSection(chunk, section);
            }
            DeserializeHeightmaps(chunk.HeightMaps, Pop<CompoundTag>("Heightmaps"));
            DeserializeTileTicks(chunk.TileTicks, Pop<ListTag>("TileTicks"));

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

                if (tag.TryGet<CompoundTag>("Properties", out var props)) {
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
            foreach (var (typeName, rawBits) in tag) {
                var packedHeights = new SparseBitStorage(256, 9, rawBits.Value<long[]>());

                var type = HeightMapType.ForName(typeName);
                var heights = maps.Get(type, true);
                for (int i = 0; i < 256; i++) {
                    heights[i] = (short)packedHeights[i];
                }
            }
        }
        private static void DeserializeTileTicks(List<ScheduledTick> list, ListTag tag)
        {
            if (tag == null) return;

            foreach (CompoundTag entry in tag) {
                var st = new ScheduledTick() {
                    Type = Block.Registry[entry.GetString("i")],
                    X = entry.GetInt("x"),
                    Y = entry.GetInt("y"),
                    Z = entry.GetInt("z"),
                    Delay = entry.GetInt("t"),
                    Priority = entry.GetInt("p")
                };
                list.Add(st);
            }
        }

        public CompoundTag Serialize(Chunk chunk)
        {
            throw new NotImplementedException();
        }

        private static void UnpackBlocks(long[] src, BlockId[] dst, BlockId[] palette)
        {
            int elemBits = GetPaletteBits(palette.Length);
            int valsPerLong = 64 / elemBits;
            int mask = (1 << elemBits) - 1;
            int srcPos = 0;

            for (int i = 0; i < dst.Length; i += valsPerLong) {
                long vals = src[srcPos++];
                int valCount = Math.Min(dst.Length - i, valsPerLong);

                for (int j = 0; j < valCount; j++) {
                    dst[i + j] = palette[vals & mask];
                    vals >>= elemBits;
                }
            }
        }
        private static int GetPaletteBits(int size)
        {
            return Math.Max(4, Maths.CeilLog2(size));
        }
    }
}
