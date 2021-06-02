using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class RegionBuffer
    {
        public readonly Chunk[] Chunks;
        public BlockPalette Palette;
        /// <summary> Number of chunks in the X/Z axis. </summary>
        public readonly int Size;
        /// <summary> Position of the first chunk in this region, in global world chunk coordinates. </summary>
        public int X, Z;

        /// <param name="count">Capacity of the buffer, in regions of 32x32 chunks.</param>
        public RegionBuffer()
        {
            Size = 32;
            Chunks = new Chunk[Size * Size];
            Palette = new BlockPalette() { BlockRegistry.Air };
        }

        /// <summary> Loads the specified region file. </summary>
        /// <param name="path">Full path of the .mca file</param>
        /// <returns>Number of non empty chunks loaded.</returns>
        public int Load(WorldInfo world, string path)
        {
            Ensure.That(Size == 32);

            Clear();
            SetPosFromRegionFile(path);
            int chunksLoaded = 0;

            using var reader = new RegionReader(path);
            foreach (var (tag, x, z) in reader.ReadAll()) {
                Chunk chunk;

                if (tag.ContainsKey("Level", TagType.Compound)) {
                    var serializer = world.GetSerializer(tag);
                    chunk = serializer.Deserialize(tag, Palette);
                } else {
                    chunk = new Chunk(X + x, Z + z, 0, -1, Palette);
                    chunk.Opaque = tag;
                    chunk.Flags |= ChunkFlags.OpaqueOnly;
                }
                Ensure.That((chunk.X & 31) == x && (chunk.Z & 31) == z, "Chunk in wrong location. Relocation is not supported");

                PutChunk(chunk);
                chunksLoaded++;
            }
            return chunksLoaded;
        }
        /// <summary> Creates a new region file with the chunks present in this buffer. </summary>
        /// <param name="path">Full path of the .mca file</param>
        public void Save(WorldInfo world, string path)
        {
            Ensure.That(Size == 32);

            using var writer = new RegionWriter(path);
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

        private void SetPosFromRegionFile(string path)
        {
            var m = Regex.Match(Path.GetFileName(path), @"^r\.(-?\d+)\.(-?\d+)\.mca$");
            if (!m.Success) {
                throw new FormatException("Region file name must have the form of 'r.0.0.mca'.");
            }
            X = int.Parse(m.Groups[1].Value) * 32;
            Z = int.Parse(m.Groups[2].Value) * 32;
        }

        public void Clear()
        {
            Chunks.Fill(null);
            Palette = new BlockPalette() { BlockRegistry.Air };
        }

        /// <summary> Calculate the min and max Y section coord in all chunks. </summary>
        public (int Min, int Max) GetChunkYExtents()
        {
            int min = int.MaxValue, max = int.MinValue;

            foreach (var chunk in Chunks) {
                if (chunk == null) continue;

                for (int y = chunk.MinSectionY; y <= chunk.MaxSectionY; y++) {
                    if (chunk.GetSection(y) != null) {
                        min = Math.Min(min, y);
                        max = Math.Max(max, y);
                    }
                }
            }
            return (min, max);
        }

        /// <summary> Removes unused entries from the chunk's block palette. </summary>
        /// <returns> The number of entries removed. </returns>
        public int OptimizePalette()
        {
            var used = new bool[Palette.Count];

            foreach (var section in ChunkIterator.GetSections(this)) {
                foreach (var block in section.Blocks) {
                    used[block] = true;
                }
            }

            int unusedCount = used.Count(false);
            if (unusedCount > 0) {
                var newId = new BlockId[Palette.Count];
                var newPalette = new BlockPalette(Palette.Count - unusedCount);

                foreach (var (block, idx) in Palette.BlocksAndIds()) {
                    if (used[idx]) {
                        newId[idx] = newPalette.Add(block);
                    }
                }
                Palette = newPalette;

                foreach (var section in ChunkIterator.GetSections(this)) {
                    section.Chunk.Palette = newPalette;
                    section.Palette = newPalette;

                    var blocks = section.Blocks;
                    for (int i = 0; i < blocks.Length; i++) {
                        blocks[i] = newId[blocks[i]];
                    }
                }
            }
            return unusedCount;
        }

        /// <summary> Gets the chunk at the specified coordinates, relative to this region. </summary>
        public Chunk GetChunk(int x, int z)
        {
            if ((uint)x >= (uint)Size || (uint)z >= (uint)Size) {
                return null;
            }
            return Chunks[x + z * Size];
        }
        public void PutChunk(Chunk chunk)
        {
            int x = chunk.X & 31;
            int z = chunk.Z & 31;
            Chunks[x + z * Size] = chunk;
        }

        public ChunkSection GetSection(int x, int y, int z)
        {
            return GetChunk(x, z)?.GetSection(y);
        }
    }
}
