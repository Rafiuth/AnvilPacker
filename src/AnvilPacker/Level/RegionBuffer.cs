﻿#nullable enable

using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class RegionBuffer
    {
        public readonly Chunk?[] Chunks;
        public BlockPalette Palette;
        /// <summary> Number of chunks in the X/Z axis. </summary>
        public readonly int Size;
        /// <summary> Position of the first chunk in this region, in global world chunk coordinates. </summary>
        public int X, Z;

        /// <summary> Data generated by reversible transforms. </summary>
        public CompoundTag? ExtraData = null;

        private string? _lastLoadedRegionFilename = null;

        /// <summary> All non-null chunks. </summary>
        public IEnumerable<Chunk> ExistingChunks => Chunks.ExceptNull();

        /// <param name="count">Capacity of the buffer, in regions of 32x32 chunks.</param>
        public RegionBuffer()
        {
            Size = 32;
            Chunks = new Chunk[Size * Size];
            Palette = new BlockPalette() { BlockRegistry.Air };
        }

        /// <returns>Number of non empty chunks loaded.</returns>
        public int Load(WorldInfo world, RegionReader reader, string? debugPath = null)
        {
            Ensure.That(Size == 32);

            Clear();
            (X, Z) = (reader.X, reader.Z);
            _lastLoadedRegionFilename = debugPath?.Replace('\\', '/');

            int chunksLoaded = 0;

            foreach (var (tag, x, z) in reader.ReadAll()) {
                Chunk chunk;

                if (tag.ContainsKey("Level", TagType.Compound)) {
                    var serializer = world.GetSerializer(tag);
                    chunk = serializer.Deserialize(tag, Palette);
                } else {
                    chunk = new Chunk(X + x, Z + z, Palette, 0, -1);
                    chunk.Opaque = tag;
                    chunk.Flags |= ChunkFlags.OpaqueOnly;
                }
                Ensure.That((chunk.X & 31) == x && (chunk.Z & 31) == z, "Chunk in wrong location. Relocation is not supported");

                PutChunk(chunk);
                chunksLoaded++;
            }
            return chunksLoaded;
        }
        public void Save(WorldInfo world, RegionWriter writer)
        {
            Ensure.That(Size == 32);

            foreach (var chunk in Chunks.ExceptNull()) {
                int cx = chunk.X & 31;
                int cz = chunk.Z & 31;
                
                if (chunk.HasFlag(ChunkFlags.OpaqueOnly)) {
                    writer.Write(cx, cz, chunk.Opaque);
                } else {
                    var serializer = world.GetSerializer(chunk);
                    var tag = serializer.Serialize(chunk);
                    writer.Write(cx, cz, tag);
                }
            }
        }

        public void Clear()
        {
            Chunks.Clear();
            Palette = new BlockPalette() { BlockRegistry.Air };
            ExtraData = null;

            _lastLoadedRegionFilename = null;
        }

        /// <summary> Calculates the min and max section Y coord in this region. </summary>
        public (int Min, int Max) GetChunkYExtents()
        {
            int min = int.MaxValue, max = int.MinValue;

            foreach (var chunk in Chunks) {
                if (chunk != null) {
                    var bounds = chunk.GetActualYExtents();
                    min = Math.Min(min, bounds.Min);
                    max = Math.Max(max, bounds.Max);
                }
            }
            return (min, max);
        }

        /// <summary> Gets the chunk at the specified coordinates, relative to this region. </summary>
        public Chunk? GetChunk(int x, int z)
        {
            if ((uint)x >= (uint)Size || (uint)z >= (uint)Size) {
                return null;
            }
            return Chunks[x + z * Size];
        }
        /// <summary> Get the chunk at the specified absolute world coordinates. </summary>
        public Chunk? GetChunkAbsCoords(int x, int z)
        {
            return GetChunk(x - X, z - Z);
        }

        public void PutChunk(Chunk chunk)
        {
            Ensure.That(IsChunkInside(chunk), "Chunk coord must be inside region");
            int x = chunk.X & 31;
            int z = chunk.Z & 31;
            Chunks[x + z * Size] = chunk;
        }

        public ChunkSection? GetSection(int x, int y, int z)
        {
            return GetChunk(x, z)?.GetSection(y);
        }

        public bool IsChunkInside(Chunk chunk)
        {
            return IsChunkInside(chunk.X, chunk.Z);
        }
        public bool IsChunkInside(int cx, int cz)
        {
            return (cx & ~31) == X && (cz & ~31) == Z;
        }
        /// <summary> Returns the region chunk index of the specified chunk (after applying modulo 32). Index = `(cx mod 32) + (cz mod 32) * 32`</summary>
        public static int GetChunkIndex(Chunk chunk)
        {
            return GetChunkIndex(chunk.X, chunk.Z);
        }
        public static int GetChunkIndex(int cx, int cz)
        {
            return (cx & 31) + (cz & 31) * 32;
        }

        public override string ToString()
        {
            return _lastLoadedRegionFilename ?? $"r.{X >> 5}.{Z >> 5}";
        }
    }
}
