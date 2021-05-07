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
        public readonly ChunkSection?[] Sections = new ChunkSection[16];
        public BlockPalette Palette;
        public HeightMaps HeightMaps = new();

        public List<ScheduledTick> ScheduledTicks = new();
        /// <summary> Data that the encoder doesn't know how to handle. Contents are left unmodified. </summary>
        public CompoundTag? Opaque { get; set; }

        /// <summary> Section Y extents, in chunk coordinates (blockPos / 16). Values are inclusive. </summary>
        public readonly int MinSectionY, MaxSectionY;

        public Chunk(int x, int z, BlockPalette palette)
        {
            X = x;
            Z = z;
            Palette = palette;
            MinSectionY = 0;
            MaxSectionY = 15;
        }

        /// <param name="y">Section Y coord (blockY >> 4)</param>
        public ChunkSection? GetSection(int y)
        {
            return (uint)y < 16u ? Sections[y] : null;
        }
        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks><see cref="IndexOutOfRangeException"/> is thrown if y is outside [0..15]</remarks>
        public void SetSection(int y, ChunkSection? section)
        {
            Sections[y] = section;
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
