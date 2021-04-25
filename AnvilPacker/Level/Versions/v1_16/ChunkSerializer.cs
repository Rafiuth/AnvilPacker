using System;
using System.Collections.Generic;
using AnvilPacker.Data;

namespace AnvilPacker.Level.Versions.v1_16
{
    public class ChunkSerializer : IChunkSerializer
    {
        public ChunkBase CreateChunk(int x, int z)
        {
            return new Chunk(x, z);
        }

        public ChunkBase Deserialize(CompoundTag tag)
        {
            int x = tag.GetInt("xPos");
            int z = tag.GetInt("zPos");
            var chunk = new Chunk(x, z);

            foreach (CompoundTag section in tag.GetList("Sections")) {
                int y = section.GetByte("Y");
                if (y < 0 || y >= 16) {
                    continue;
                }
                chunk.SetSection(y, DeserializeSection(section));
            }
            chunk.IsLightPopulated = Pop<bool>("LightPopulated");
            chunk.IsTerrainPopulated = Pop<bool>("TerrainPopulated");
            chunk.LastUpdate = Pop<long>("LastUpdate");
            chunk.InhabitedTime = Pop<long>("InhabitedTime");
            //chunk.HeightMap = Pop<int[]>("HeightMap");
            //chunk.Biomes = Pop<byte[]>("Biomes");

            chunk.TileTicks = DeserializeTileTicks(Pop<ListTag>("TileTicks"));

            chunk.OpaqueData = tag;

            return chunk;

            T Pop<T>(string name)
            {
                var val = tag[name];
                tag.Remove(name);

                if (val is T) {
                    return (T)(object)val;
                } else if (val is PrimitiveTag pt) {
                    return pt.Value<T>();
                } else {
                    return default;
                }
            }
        }


        private static ChunkSectionBase DeserializeSection(CompoundTag tag)
        {
            var blockStates = tag.GetLongArray("BlockStates");
            var palette = ReadPalette(tag.GetList("Palette"));
            
            var skyLight = tag.GetByteArray("SkyLight");
            var blockLight = tag.GetByteArray("BlockLight");

            if (blockStates == null || (palette.Count == 1 && palette.GetState(0) == BlockState.Air)) {
                return null;
            }
            var section = new ChunkSection(palette, blockStates);

            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }
            return section;
        }

        private static BlockPalette ReadPalette(ListTag list)
        {
            if (list == null) return null;

            var palette = new BlockPalette();
            foreach (CompoundTag tag in list) {
                var name = tag.GetString("Name");
                var props = tag.GetCompound("Properties", true);

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

        public CompoundTag Serialize(ChunkBase chunk)
        {
            throw new NotImplementedException();
        }
    }
}
