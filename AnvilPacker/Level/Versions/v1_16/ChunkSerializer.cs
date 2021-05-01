using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Versions.v1_16
{
    /// <summary> Handles chunks serialization for versions <c>1.16-1.16.5</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        public Chunk Deserialize(CompoundTag tag)
        {
            int x = tag.GetInt("xPos");
            int z = tag.GetInt("zPos");
            var chunk = new Chunk(x, z);

            foreach (CompoundTag section in tag.GetList("Sections")) {
                DeserializeSection(chunk, section);
            }
            //chunk.IsLightPopulated = Pop<bool>("LightPopulated");
            //chunk.IsTerrainPopulated = Pop<bool>("TerrainPopulated");
            //chunk.LastUpdate = Pop<long>("LastUpdate");
            //chunk.InhabitedTime = Pop<long>("InhabitedTime");
            //chunk.HeightMap = Pop<int[]>("HeightMap");
            //chunk.Biomes = Pop<byte[]>("Biomes");

            chunk.TileTicks = DeserializeTileTicks(Pop<ListTag>("TileTicks"));

            chunk.Opaque = tag;

            return chunk;

            T Pop<T>(string name)
            {
                var val = tag.Get<T>(name, TagGetMode.Null);
                tag.Remove(name);

                return val;
            }
        }


        private static void DeserializeSection(Chunk chunk, CompoundTag tag)
        {
            int y = tag.GetSByte("Y");
            if (y < 0 || y >= 16) {
                return;
            }
            var blockStates = tag.GetLongArray("BlockStates", TagGetMode.Null);
            var palette = DeserializePalette(tag.GetList("Palette", TagGetMode.Null));
            
            var skyLight = tag.GetByteArray("SkyLight", TagGetMode.Null);
            var blockLight = tag.GetByteArray("BlockLight", TagGetMode.Null);

            if (blockStates == null || (palette.Count == 1 && palette.GetState(0) == BlockState.Air)) {
                return;
            }
            var section = chunk.GetOrCreateSection(y, palette);
            UnpackBits(blockStates, section.Blocks, GetPaletteBits(palette));

            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }
        }

        private static BlockPalette DeserializePalette(ListTag list)
        {
            if (list == null) return null;

            var palette = new BlockPalette();
            foreach (CompoundTag tag in list) {
                var name = tag.GetString("Name");
                var props = tag.GetCompound("Properties", TagGetMode.Null);

                var state = Block.Registry[name].DefaultState;

                if (props != null) {
                    foreach (var (k, v) in props) {
                        state = state.WithProperty(k, v.Value<string>());
                    }
                }
                palette.Add(state);
            }
            return palette;
        }

        private static List<ScheduledTick> DeserializeTileTicks(ListTag tag)
        {
            var list = new List<ScheduledTick>();

            if (tag == null) {
                return list;
            }
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
            return list;
        }

        public CompoundTag Serialize(Chunk chunk)
        {
            throw new NotImplementedException();
        }

        //1.16 bit storage is sparse, elements will never span along multiple longs.
        //bit[i] = (data[i / elemBits] >> (i % elemBits)) & mask
        private static void UnpackBits(long[] src, BlockId[] dst, int elemBits)
        {
            int valsPerLong = 64 / elemBits;
            int mask = (1 << elemBits) - 1;
            int srcPos = 0;

            for (int i = 0; i < dst.Length; i += valsPerLong) {
                long vals = src[srcPos++];
                int valCount = Math.Min(dst.Length - i, valsPerLong);

                for (int j = 0; j < valCount; j++) {
                    dst[i + j] = (BlockId)(vals & mask);
                    vals >>= elemBits;
                }
            }
        }
        private static int GetPaletteBits(BlockPalette palette)
        {
            return Math.Max(4, Maths.CeilLog2(palette.Count));
        }
    }
}
