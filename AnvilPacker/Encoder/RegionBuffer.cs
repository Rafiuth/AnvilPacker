using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class RegionBuffer
    {
        public readonly ChunkBase[] Chunks;
        /// <summary> Number of chunks in the X/Z axis. </summary>
        public readonly int Width, Depth;
        /// <summary> Position of the first chunk in this region, in global world chunk coordinates. </summary>
        public int X, Z;

        public RegionBuffer(int w, int d)
        {
            Chunks = new ChunkBase[w * d];
            Width = w;
            Depth = d;
        }

        public ChunkBase GetChunk(int x, int z)
        {
            if ((uint)x >= (uint)Width || (uint)z >= (uint)Depth) {
                return null;
            }
            return Chunks[x + z * Width];
        }
        public void SetChunk(int x, int z, ChunkBase chunk)
        {
            if ((uint)x >= (uint)Width || (uint)z >= (uint)Depth) {
                throw new ArgumentOutOfRangeException();
            }
            Chunks[x + z * Width] = chunk;
        }

        /// <summary> Creates a enumerator of chunks in the specified region. </summary>
        /// <remarks>The enumerator does not yield non-existent chunks.</remarks>
        public IEnumerable<ChunkIteratorData> GetChunks(Vec3i start, Vec3i end)
        {
            var startChunk = start >> 4;
            var endChunk = end >> 4;

            for (int z = startChunk.Z; z <= endChunk.Z; z++) {
                for (int x = startChunk.X; x <= endChunk.X; x++) {
                    var chunk = GetChunk(x, z);
                    if (chunk == null) continue;

                    for (int y = startChunk.Y; y <= endChunk.Y; y++) {
                        var section = chunk.GetSection(y);
                        if (section == null) continue;

                        var pos = new Vec3i(x, y, z);

                        yield return new ChunkIteratorData() {
                            Chunk   = chunk,
                            Section = section,
                            Start   = Vec3i.Max(start, pos << 4),
                            End     = Vec3i.Min(end, (pos << 4) + 16)
                        };
                    }
                }
            }
        }

        public struct ChunkIteratorData
        {
            public ChunkBase Chunk;
            public ChunkSectionBase Section;

            /// <summary> Global position of the first block in this chunk. </summary>
            public Vec3i Start;
            /// <summary> Global position of the last block in this chunk. (exclusive) </summary>
            public Vec3i End;

            public int X0 => Start.X;
            public int Y0 => Start.Y;
            public int Z0 => Start.Z;
            public int X1 => End.X;
            public int Y1 => End.Y;
            public int Z1 => End.Z;

            /// <summary> Returns the block at the specified global coord. </summary>
            public BlockState GetBlock(int x, int y, int z)
            {
                return Section.GetBlock(x & 15, y & 15, z & 15);
            }
        }
    }
}
