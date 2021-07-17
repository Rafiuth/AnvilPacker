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

        public void Compute(Chunk chunk, bool[] isBlockOpaque)
        {
            var heights = Values;
            var sections = chunk.Sections;

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    for (int si = sections.Length - 1; si >= 0; si--) {
                        var section = sections[si];
                        if (section == null) continue;

                        for (int y = 15; y >= 0; y--) {
                            var block = section.GetBlockId(x, y, z);
                            if (isBlockOpaque[block]) {
                                heights[x + z * 16] = (short)(section.Y * 16 + y + 1);
                                goto ColumnDone;
                            }
                        }
                    }
                    //Should only reach here if the entire column was empty.
                    heights[x + z * 16] = MinY;
                    ColumnDone:;
                }
            }
        }
    }
}