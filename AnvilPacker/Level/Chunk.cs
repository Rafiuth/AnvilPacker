#nullable enable

using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Chunk
    {
        public readonly int X, Z;
        /// <summary> Section Y extents, in chunk coordinates (blockPos / 16). Values are inclusive. </summary>
        public readonly int MinSectionY, MaxSectionY;
        public readonly ChunkSection?[] Sections;
        public BlockPalette Palette;
        public HeightMaps HeightMaps = new();

        public List<ScheduledTick> ScheduledTicks = new();
        /// <summary> Data that the serializer doesn't know how to handle. This is the "Level" tag from the region chunk. </summary>
        public CompoundTag? Opaque;
        public int DataVersion;
        public bool HasLightData;

        public Chunk(int x, int z, int minSy, int maxSy, BlockPalette palette)
        {
            X = x;
            Z = z;
            MinSectionY = minSy;
            MaxSectionY = maxSy;
            Sections = new ChunkSection[MaxSectionY - MinSectionY + 1];
            Palette = palette;
        }

        /// <param name="y">Section Y coord (blockY >> 4)</param>
        public ChunkSection? GetSection(int y)
        {
            if (y >= MinSectionY && y <= MaxSectionY) {
                return Sections[y - MinSectionY];
            }
            return null;
        }
        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks><see cref="IndexOutOfRangeException"/> is thrown if y is outside Y limits.</remarks>
        public void SetSection(int y, ChunkSection? section)
        {
            Sections[y - MinSectionY] = section;
        }
        public ChunkSection GetOrCreateSection(int y)
        {
            var section = GetSection(y);
            if (section == null) {
                Ensure.That(y >= MinSectionY && y <= MaxSectionY, "Cannot create section outside world Y bounds.");

                section = new ChunkSection(this, y);
                SetSection(y, section);
            }
            return section;
        }

        /// <summary> Computes the min and max non-empty section Y coords. </summary>
        /// <remarks> If this chunk is fully empty, the result is (MinSectionY, MinSectionY-1). (max &lt; min) </remarks>
        public (int Min, int Max) GetActualYExtents()
        {
            int min = MinSectionY;
            int max = MaxSectionY;
            //Find max first, so if this chunk is empty, the result will be (minSy, minSy-1).
            while (max >= min && GetSection(max) == null) max--;
            while (min <= max && GetSection(min) == null) min++;

            return (min, max);
        }

        public BlockState GetBlock(int x, int y, int z)
        {
            var sect = GetSection(y >> 4);
            return sect != null ? sect.GetBlock(x, y & 15, z) : BlockState.Air;
        }
        public void SetBlock(int x, int y, int z, BlockState block)
        {
            var sect = GetOrCreateSection(y >> 4);
            sect.SetBlock(x, y & 15, z, block);
        }
    }

    public struct ScheduledTick
    {
        /// <summary> Block X/Z position, relative to the chunk this tick was scheduled into. </summary>
        public sbyte X, Z;
        /// <summary> Block Y position, in absolute world coordinates. </summary>
        public short Y;
        public int Delay;
        public int Priority;
        public Block Type;
    }
}
