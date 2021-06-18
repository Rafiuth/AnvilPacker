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
        public short YOffset;

        public int this[int x, int z]
        {
            get => Values[x + z * 16] + YOffset;
            set => Values[x + z * 16] = (short)(value - YOffset);
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

            var (minSy, maxSy) = chunk.GetActualYExtents();

            var heights = heightmap.Values;

            int defaultHeight = minSy * 16;
            heights.Fill((short)defaultHeight);

            var populated = _populated;
            populated.Clear();

            int numPopulated = 0;

            for (int sy = maxSy; sy >= minSy; sy--) {
                var section = chunk.GetSection(sy);
                if (section == null) continue;

                for (int y = 15; y >= 0; y--) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            var block = section.GetBlockId(x, y, z);
                            int index = x + z * 16;
                            if (_isOpaque[block] && !populated[index]) {
                                heights[index] = (short)(sy * 16 + y + 1);
                                populated[index] = true;
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