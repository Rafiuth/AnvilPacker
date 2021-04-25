using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Level;
using NLog;
using static AnvilPacker.Util.Maths;

namespace AnvilPacker.Level.Versions.v1_8
{
    //quick ref: https://minecraft.gamepedia.com/Chunk_format?oldid=1229175
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public ChunkBase CreateChunk(int x, int z)
        {
            return new Chunk(x, z);
        }

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
            //chunk.HeightMap = Pop<int[]>("HeightMap");
            //chunk.Biomes = Pop<byte[]>("Biomes");

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

        private ChunkSectionBase DeserializeSection(CompoundTag tag)
        {
            var blockId = tag.GetByteArray("Blocks");
            var blockAdd = tag.GetByteArray("Add");
            var blockData = tag.GetByteArray("Data");

            var skyLight = tag.GetByteArray("SkyLight");
            var blockLight = tag.GetByteArray("BlockLight");

            var section = new ChunkSection();
            
            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }

            var blocks = section.Blocks;

            for (int i = 0; i < 4096; i += 2) {
                int j = i >> 1;
                int a = (blockId[i + 0] << 4) | (blockData[j] & 15);
                int b = (blockId[i + 1] << 4) | (blockData[j] >> 4);

                if (blockAdd != null) {
                    a |= (blockAdd[j] & 15) << 12;
                    b |= (blockAdd[j] >> 4) << 12;
                }
                blocks[i + 0] = (ushort)a;
                blocks[i + 1] = (ushort)b;
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
                Block type;

                if (entry.ContainsKey("i", TagType.String)) {
                    type = LegacyBlocks.GetBlockFromName(entry.GetString("i"));
                } else {
                    type = LegacyBlocks.GetBlockFromId(entry.GetInt("i"));
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
