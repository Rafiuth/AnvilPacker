using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level.Versions.v1_2_1
{
    //quick ref: https://minecraft.gamepedia.com/Chunk_format?oldid=1229175
    /// <summary> Handles chunks serialization for versions <c>1.2.1-1.12.2</c>. </summary>
    public class ChunkSerializer : IChunkSerializer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public Chunk Deserialize(CompoundTag rootTag, BlockPalette palette)
        {
            var tag = rootTag.GetCompound("Level");

            int x = tag.Pop<int>("xPos");
            int z = tag.Pop<int>("zPos");
            var chunk = new Chunk(x, z, palette);
            chunk.Opaque = rootTag;
            chunk.DataVersion = (DataVersion)rootTag.PopMaybe<int>("DataVersion");

            if (tag.TryGet("Sections", out ListTag sectList)) {
                var localPalette = new DictionarySlim<ushort, BlockId>(64);

                for (int i = 0; i < sectList.Count; i++) {
                    var sectTag = sectList.Get<CompoundTag>(i);
                    DeserializeSection(chunk, sectTag, localPalette);
                    if (sectTag.Count <= 1) { //is "Y" the only value left?
                        sectList.RemoveAt(i--);
                    }
                }
                if (sectList.Count == 0) {
                    tag.Remove("Sections");
                }
            }
            DeserializeHeightmap(chunk, tag.PopMaybe<int[]>("HeightMap"));
            chunk.SetFlag(ChunkFlags.LightDirty, !tag.PopMaybe<bool>("LightPopulated"));

            return chunk;
        }

        private unsafe void DeserializeSection(Chunk chunk, CompoundTag tag, DictionarySlim<ushort, BlockId> localPalette)
        {
            int y = tag.GetSByte("Y");

            var section = chunk.GetOrCreateSection(y);

            var blockId = tag.Pop<byte[]>("Blocks");
            var blockData = tag.Pop<byte[]>("Data");
            var blockAdd = tag.PopMaybe<byte[]>("Add");

            var skyLight = tag.PopMaybe<byte[]>("SkyLight");
            var blockLight = tag.PopMaybe<byte[]>("BlockLight");
            
            if (skyLight != null) {
                section.SkyLight = new NibbleArray(skyLight);
            }
            if (blockLight != null) {
                section.BlockLight = new NibbleArray(blockLight);
            }

            var globalPalette = section.Palette;
            var blocks = section.Blocks;

            const int CacheKeyMask = 7;
            const int CacheSize = CacheKeyMask + 1;
            //using int instead of ushort because it will avoid undefined
            //behavior if we find a block with the invalid id (65535).
            var idCacheKeys = stackalloc int[CacheSize];
            var idCacheVals = stackalloc BlockId[CacheSize];
            new Span<int>(idCacheKeys, CacheSize).Fill(-1);  //mark all cache keys as invalid
            
            for (int i = 0; i < 4096; i += 2) {
                int j = i >> 1;
                int a = (blockId[i + 0] << 4) | (blockData[j] & 15);
                int b = (blockId[i + 1] << 4) | (blockData[j] >> 4);

                if (blockAdd != null) {
                    a |= (blockAdd[j] & 15) << 12;
                    b |= (blockAdd[j] >> 4) << 12;
                }
                blocks[i + 0] = GetId((ushort)a);
                blocks[i + 1] = GetId((ushort)b);
            }

            if (localPalette.Count == 1 && globalPalette.GetState(localPalette.First().Value).Material == BlockMaterial.Air) {
                chunk.SetSection(y, null);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            BlockId GetId(ushort stateId)
            {
                int cacheIdx = stateId & CacheKeyMask;
                if (idCacheKeys[cacheIdx] == stateId) {
                    return idCacheVals[cacheIdx];
                }
                return GetIdUncached(stateId);
            }
            BlockId GetIdUncached(ushort stateId)
            {
                if (localPalette.TryGetValue(stateId, out var id)) {
                    int cacheIdx = stateId & CacheKeyMask;
                    idCacheKeys[cacheIdx] = stateId;
                    idCacheVals[cacheIdx] = id;
                    return id;
                }
                return GetIdSlow(stateId);
            }
            BlockId GetIdSlow(ushort stateId)
            {
                var state = BlockRegistry.GetLegacyState(stateId);
                var id = globalPalette.GetOrAddId(state);
                localPalette.Add(stateId, id);
                return id;
            }
        }
        private void DeserializeHeightmap(Chunk chunk, int[] heights)
        {
            var heightmap = new Heightmap();
            for (int i = 0; i < 256; i++) {
                heightmap.Values[i] = (short)heights[i];
            }
            chunk.Heightmaps.Add(Heightmap.TYPE_LEGACY, heightmap);
        }

        public CompoundTag Serialize(Chunk chunk)
        {
            var tag = new CompoundTag();
            tag.SetInt("xPos", chunk.X);
            tag.SetInt("zPos", chunk.Z);

            var sections = new ListTag();
            var stateIds = CreateBlockReindexTable(chunk.Palette);

            foreach (var section in chunk.Sections.ExceptNull()) {
                Debug.Assert(section.Palette == chunk.Palette);
                sections.Add(SerializeSection(section, stateIds));
            }
            tag.SetList("Sections", sections);
            tag.SetIntArray("HeightMap", SerializeHeightmap(chunk));

            //TODO: what this field does?
            tag.SetBool("LightPopulated", !chunk.HasFlag(ChunkFlags.LightDirty));

            var rootTag = new CompoundTag();
            rootTag.SetCompound("Level", tag);
            rootTag.SetInt("DataVersion", (int)chunk.DataVersion);
            ChunkNbtUtils.MergeOpaque(chunk.Opaque, rootTag);

            return rootTag;
        }

        private int[] SerializeHeightmap(Chunk chunk)
        {
            if (!chunk.Heightmaps.TryGetValue(Heightmap.TYPE_LEGACY, out var map)) {
                throw new NotSupportedException("Legacy chunk serializer requires heightmap to be present.");
            }

            short[] src = map.Values;
            int[] dst = new int[256];
            for (int i = 0; i < 256; i++) {
                dst[i] = src[i];
            }
            return dst;
        }

        const ushort INVALID_STATE_ID = 65535;
        
        private CompoundTag SerializeSection(ChunkSection section, ushort[] stateIds)
        {
            var blockId = new byte[4096];
            var blockData = new byte[2048];
            var blockAdd = new byte[2048];

            var blocks = section.Blocks;
            int hasAdd = 0;
            bool hasModernStateRefs = false;

            for (int i = 0; i < 4096; i += 2) {
                int j = i >> 1;
                ushort a = stateIds[blocks[i + 0]];
                ushort b = stateIds[blocks[i + 1]];

                blockId[i + 0] = (byte)(a >> 4);
                blockId[i + 1] = (byte)(b >> 4);

                blockData[j] = (byte)((a & 15) | (b & 15) << 4);

                int add = (a >> 12) | (b >> 12) << 4;
                blockAdd[j] = (byte)add;
                hasAdd |= add;
                hasModernStateRefs |= a == INVALID_STATE_ID || b == INVALID_STATE_ID;
            }
            Ensure.That(!hasModernStateRefs, "Legacy chunk contains references to modern block states.");

            var tag = new CompoundTag();
            tag.SetSByte("Y", (sbyte)section.Y);
            tag.SetByteArray("Blocks", blockId);
            tag.SetByteArray("Data", blockData);
            if (hasAdd != 0) {
                tag.SetByteArray("Add", blockAdd);
            }
            tag.SetByteArray("SkyLight", section.SkyLight?.Data ?? new byte[2048]);
            tag.SetByteArray("BlockLight", section.BlockLight?.Data ?? new byte[2048]);
            return tag;
        }
        private ushort[] CreateBlockReindexTable(BlockPalette palette)
        {
            return palette.ToArray(b => {
                if (b == BlockRegistry.Air) {
                    return (ushort)0;
                }
                if (!b.HasAttrib(BlockAttributes.Legacy)) {
                    return INVALID_STATE_ID;
                }
                return (ushort)b.Id;
            });
        }
    }
}
