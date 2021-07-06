#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Heightmap
    {
        public const string TYPE_LEGACY = "_LEGACY";

        public readonly short[] Values = new short[16 * 16];
        public readonly short MinY;

        public Heightmap(int minY = 0)
        {
            MinY = (short)minY;
        }

        public int this[int x, int z]
        {
            get => Values[x + z * 16];
            set => Values[x + z * 16] = (short)value;
        }
    }
    /// <summary> Provides methods for computing heightmaps for chunks in a region. </summary>
    public class HeightmapComputer
    {
        private RegionBuffer _region;
        private string _type;
        private bool[] _isOpaque;
        private BitSet _populated;

        public HeightmapComputer(RegionBuffer region, string type, bool[] isBlockOpaque)
        {
            _region = region;
            _type = type;
            _populated = new BitSet(16 * 16);
            _isOpaque = isBlockOpaque;
        }
        public void Compute(Chunk chunk)
        {
            var heightmap = chunk.Heightmaps.GetOrAdd(_type) ??= new();
            Compute(chunk, heightmap);
        }
        public void Compute(Chunk chunk, Heightmap heightmap)
        {
            Ensure.That(chunk.Palette == _region.Palette, "Chunk not in region");

            var heights = heightmap.Values;
            heights.Fill(heightmap.MinY);

            var populated = _populated;
            populated.Clear();

            int numPopulated = 0;
            var sections = chunk.Sections;

            for (int i = sections.Length - 1; i >= 0; i--) {
                var section = sections[i];
                if (section == null) continue;

                for (int y = 15; y >= 0; y--) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            var block = section.GetBlockId(x, y, z);
                            int index = x + z * 16;
                            if (_isOpaque[block] && populated.Add(index)) {
                                heights[index] = (short)(section.Y * 16 + y + 1);
                                numPopulated++;
                            }
                        }
                    }
                }
                if (numPopulated >= 16 * 16) break;
            }
        }
    }
}