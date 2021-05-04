using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public RegionBuffer(int count = 1)
        {
            Ensure.That(count > 0);
            Size = count * 32;
            Chunks = new Chunk[Size * Size];
            Palette = new BlockPalette() { BlockState.Air };
        }

        /// <summary> Fills the buffer with the chunks at the specified region offset. </summary>
        public void Load(WorldInfo world, string regionPath, int rx, int rz)
        {
            string path = Path.Combine(world.Path, regionPath);

            Clear();
            for (int z = 0; z < Size; z++) {
                for (int x = 0; x < Size; x++) {
                    LoadChunks(world, path, rx + x, rz + z);
                }
            }
        }
        private void LoadChunks(WorldInfo world, string path, int rx, int rz)
        {
            using var reader = new AnvilReader(path, rx, rz);
            for (int cz = 0; cz < 32; cz++) {
                for (int cx = 0; cx < 32; cx++) {
                    Chunk chunk = null;

                    var tag = reader.Read(cx, cz);
                    if (tag != null) {
                        var serializer = world.GetSerializer(tag);
                        chunk = serializer.Deserialize(tag, Palette);
                    }
                    SetChunk(rx * 32 + cx, rz * 32 + cz, chunk);
                }
            }
        }

        public void Clear()
        {
            Chunks.Fill(null);
            Palette = new BlockPalette() { BlockState.Air };
        }

        /// <summary> Calculate the min and max Y section coord in all chunks. </summary>
        public (int Min, int Max) GetChunkYExtents()
        {
            int min = int.MaxValue, max = int.MinValue;

            foreach (var chunk in Chunks){
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

        public Chunk GetChunk(int x, int z)
        {
            if ((uint)x >= (uint)Size || (uint)z >= (uint)Size) {
                return null;
            }
            return Chunks[x + z * Size];
        }
        public void SetChunk(int x, int z, Chunk chunk)
        {
            if ((uint)x >= (uint)Size || (uint)z >= (uint)Size) {
                throw new ArgumentOutOfRangeException();
            }
            Chunks[x + z * Size] = chunk;
        }

        public ChunkSection GetSection(int x, int y, int z)
        {
            return GetChunk(x, z)?.GetSection(y);
        }
    }
}
