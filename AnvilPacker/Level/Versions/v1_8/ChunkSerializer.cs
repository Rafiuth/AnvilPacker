using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Level.Serializer;
using NLog;
using static AnvilPacker.Util.Maths;

namespace AnvilPacker.Level.Versions.v1_8
{
    //quick ref: https://minecraft.gamepedia.com/Chunk_format?oldid=1229175
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public ChunkBase Deserialize(CompoundTag level)
        {
            level = new CompoundTag(level);

            int x = Pop<int>("xPos");
            int z = Pop<int>("zPos");
            var chunk = new Chunk(x, z);

            foreach (CompoundTag section in Pop<ListTag>("Sections")) {
                int y = section.GetByte("Y");
                chunk.SetSection(y, DeserializeSection(section));
            }
            chunk.IsLightPopulated = Pop<bool>("LightPopulated");
            chunk.IsTerrainPopulated = Pop<bool>("TerrainPopulated");
            chunk.LastUpdate = Pop<long>("LastUpdate");
            chunk.InhabitedTime = Pop<long>("InhabitedTime");
            chunk.HeightMap = Pop<int[]>("HeightMap");
            chunk.Biomes = Pop<byte[]>("Biomes");

            chunk.TileTicks = DeserializeTileTicks(x, z, Pop<ListTag>("TileTicks"));

            chunk.OpaqueData = level;

            return chunk;

            T Pop<T>(string name)
            {
                var tag = level[name];
                level.Remove(name);

                if (tag is T) {
                    return (T)(object)tag;
                } else if (tag is PrimitiveTag pt) {
                    return pt.Value<T>();
                } else {
                    return default;
                }
            }
        }

        private IChunkSection DeserializeSection(CompoundTag tag)
        {
            var blockId = tag.GetByteArray("Blocks");
            var blockAdd = tag.GetByteArray("Add");
            var blockData = tag.GetByteArray("Data");

            var skyLight = tag.GetByteArray("SkyLight");
            var blockLight = tag.GetByteArray("BlockLight");

            var section = new ChunkSection();
            var blocks = section.BlockData;
            var lights = section.LightData;

            for (int i = 0; i < 4096; i++) {
                int id = blockId[i] << 4 | GetNibble(blockData, i);
                if (blockAdd != null) {
                    id |= GetNibble(blockAdd, i) << 12;
                }
                blocks[i] = (ushort)id;

                lights[i] = new LightState(
                    (skyLight != null ? GetNibble(skyLight,   i) : 0) |
                    (                   GetNibble(blockLight, i)) << 4
                );
            }
            return section;
        }

        private List<ScheduledTick> DeserializeTileTicks(int cx, int cz, ListTag tag)
        {
            var list = new List<ScheduledTick>();

            if (tag == null) {
                return list;
            }
            foreach (CompoundTag entry in tag) {
                LBlock type;

                if (entry.ContainsKey("i", TagType.String)) {
                    type = LBlock.GetFromName(entry.GetString("i"));
                } else {
                    type = LBlock.GetFromId(entry.GetInt("i"));
                }

                var st = new ScheduledTick() {
                    X = entry.GetInt("x"),
                    Y = entry.GetInt("y"),
                    Z = entry.GetInt("z"),
                    Delay = entry.GetInt("t"),
                    Priority = entry.GetInt("p"),
                    Type = type
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
