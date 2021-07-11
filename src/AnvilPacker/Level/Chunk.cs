#nullable enable

using System;
using System.Collections.Generic;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Chunk
    {
        public const int MIN_ALLOWED_SECTION_Y = -32, MAX_ALLOWED_SECTION_Y = 31;

        public readonly int X, Z;
        /// <summary> Section Y extents, in chunk coordinates (blockPos / 16). Values are inclusive. </summary>
        public int MinSectionY, MaxSectionY;
        public ChunkSection?[] Sections;
        public BlockPalette Palette;
        public DictionarySlim<string, Heightmap> Heightmaps = new();

        /// <summary> Data that the serializer doesn't know how to handle. This is the root tag from the region chunk. </summary>
        public CompoundTag? Opaque;
        public int DataVersion;
        public ChunkFlags Flags;

        public Chunk(int x, int z, BlockPalette palette, int initialMinY = 0, int initialMaxY = 8)
        {
            X = x;
            Z = z;
            MinSectionY = initialMinY;
            MaxSectionY = initialMaxY;
            Sections = new ChunkSection[MaxSectionY - MinSectionY + 1];
            Palette = palette;
        }

        public bool HasFlag(ChunkFlags mask)
        {
            return (Flags & mask) == mask;
        }
        public void SetFlag(ChunkFlags mask, bool value = true)
        {
            if (value) {
                Flags |= mask;
            } else {
                Flags &= ~mask;
            }
        }

        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks>null is returned if y is outside the chunk height.</remarks>
        public ChunkSection? GetSection(int y)
        {
            if (y >= MinSectionY && y <= MaxSectionY) {
                return Sections[y - MinSectionY];
            }
            return null;
        }
        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks><see cref="IndexOutOfRangeException"/> is thrown if y is outside the chunk height.</remarks>
        public void SetSection(int y, ChunkSection? section)
        {
            Sections[y - MinSectionY] = section;
        }

        public ChunkSection GetOrCreateSection(int sy)
        {
            var section = GetSection(sy);
            if (section == null) {
                EnsureSectionFits(sy);

                section = new ChunkSection(this, sy);
                SetSection(sy, section);
            }
            return section;
        }

        private void EnsureSectionFits(int sy)
        {
            Ensure.That(sy >= MIN_ALLOWED_SECTION_Y && sy <= MAX_ALLOWED_SECTION_Y, "Section outside allowed Y bounds.");

            const int GROW_STEP = 4;

            if (sy < MinSectionY) {
                MinSectionY = Math.Max(MIN_ALLOWED_SECTION_Y, sy - GROW_STEP);
            }
            if (sy > MaxSectionY) {
                MaxSectionY = Math.Min(MAX_ALLOWED_SECTION_Y, sy + GROW_STEP);
            }
            int newHeight = MaxSectionY - MinSectionY + 1;
            
            if (Sections.Length < newHeight) {
                var newSections = new ChunkSection[newHeight];
                foreach (var section in Sections) {
                    if (section != null) {
                        newSections[section.Y - MinSectionY] = section;
                    }
                }
                Sections = newSections;
            }
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
            return sect != null ? sect.GetBlock(x, y & 15, z) : BlockRegistry.Air;
        }
        public void SetBlock(int x, int y, int z, BlockState block)
        {
            SetBlockId(x, y, z, Palette.GetOrAddId(block));
        }
        public void SetBlockId(int x, int y, int z, BlockId id)
        {
            var sect = GetOrCreateSection(y >> 4);
            sect.SetBlockId(x, y & 15, z, id);
        }
    }
    [Flags]
    public enum ChunkFlags
    {
        //Warn: don't change values, they are transmitted by the codec.
        None            = 0,
        /// <summary> Only <see cref="Chunk.Opaque" /> should be serialized. </summary>
        OpaqueOnly      = 1 << 0,
        /// <summary> 
        /// Indicates that light data should be recomputed. 
        /// When this flag is present, the serializer will set, depending on the version, `isLightOn` or `LightPopulated` to 0.
        /// </summary>
        LightDirty      = 1 << 1,
    }
}
