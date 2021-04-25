using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder.Transforms
{
    /// <summary> Replace occluded blocks with their most frequent neighbor. </summary>
    //XXXX    XXXX
    //XYZX -> XXXX
    //XXXX    XXXX
    public class HiddenBlockRemovalTransform : BlockTransform
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary> Specifies how many blocks to search for the replacement. </summary>
        public int Samples = 8;
        /// <summary> Specifies the maximum replacement search distance. </summary>
        public int Radius = 2;
        /// <summary> Don't clear frequency table for each block. If enabled, fewer samples can be used. Generally improves compression. </summary>
        public bool CummulativeFreqs = true;

        // S | R | Cum | File Size | time
        //    off      | 1074.439KB| 3s
        //16 | 2 | no  | 542.131KB | 12s
        //24 | 2 | no  | 426.957KB | 13s
        //32 | 2 | no  | 381.824KB | 15s
        // 8 | 2 | yes | 335.672KB | 8s
        //16 | 2 | yes | 335.139KB | 10s

        private static readonly Vec3i[] SelfAndImmediateNeighbors = {
            new(0, 0, 0),
            new(-1, 0, 0),
            new(+1, 0, 0),
            new(0, -1, 0),
            new(0, +1, 0),
            new(0, 0, -1),
            new(0, 0, +1),
        };

        public override void Apply(CodingUnit unit)
        {
            int size = unit.Size;
            Debug.Assert(Maths.IsPow2(size)); //because our (uint)+OR bounds check below

            var palette = unit.Palette;
            var isOpaque = BuildOpaquenessTable(palette);
            var freqs = new int[palette.Length];
            ushort mostFrequent = 0;

            var neighbors = GetNeighbors();

            int simplifiedCount = 0;
            //exclude borders because we only ever know one unit.
            for (int y = 1; y < size - 1; y++) {
                for (int z = 1; z < size - 1; z++) {
                    for (int x = 1; x < size - 1; x++) {
                        if (!CummulativeFreqs) {
                            //TODO: this will become expansive if palette is too big. Maybe use a Dictionary?
                            freqs.Clear();
                        }
                        if (!IsHidden(x, y, z)) continue;

                        foreach (var pos in neighbors) {
                            int nx = x + pos.X;
                            int ny = y + pos.Y;
                            int nz = z + pos.Z;

                            if ((uint)(nx | ny | nz) >= (uint)size) continue;

                            UpdateFreq(unit.GetBlock(nx, ny, nz));
                        }
                        if (freqs[mostFrequent] >= 2 && mostFrequent != unit.GetBlock(x, y, z)) {
                            unit.SetBlock(x, y, z, mostFrequent);
                            simplifiedCount++;
                        }
                    }
                }
                freqs.Clear(); //forget old freqs when CummulativeFreqs == true
            }
            _logger.Debug($"Removed {simplifiedCount} hidden blocks. ({simplifiedCount * 100L / (size * size * size)}%) blocks.");

            //Check if the block is surrounded by opaque blocks
            bool IsHidden(int x, int y, int z)
            {
                foreach (var pos in SelfAndImmediateNeighbors) {
                    ushort id = unit.GetBlock(x + pos.X, y + pos.Y, z + pos.Z);
                    if (!isOpaque[id]) {
                        return false;
                    }
                    UpdateFreq(id);
                }
                return true;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void UpdateFreq(ushort id)
            {
                freqs[id]++;
                if (freqs[id] > freqs[mostFrequent]) {
                    mostFrequent = id;
                }
            }
        }

        private static bool[] BuildOpaquenessTable(BlockState[] palette)
        {
            var isOpaque = new bool[palette.Length];
            for (int i = 0; i < palette.Length; i++) {
                //Attributes required to be true
                const BlockAttributes AttrMaskT = BlockAttributes.OpaqueFullCube;
                //Attributes required to be false
                const BlockAttributes AttrMaskF = BlockAttributes.Translucent;

                var attrs = palette[i].Attributes;
                isOpaque[i] = (attrs & (AttrMaskT | AttrMaskF)) == AttrMaskT;
            }
            return isOpaque;
        }

        private Vec3i[] GetNeighbors()
        {
            int r = Radius;
            int rs = r * 2 + 1;
            var points = new List<Vec3i>();

            for (int y = -r; y <= r; y++) {
                for (int z = -r; z <= r; z++) {
                    for (int x = -r; x <= r; x++) {
                        points.Add(new Vec3i(x, y, z));
                    }
                }
            }
            var rng = new Random(12345);
            points.Shuffle(rng.Next);
            //TODO: ensure points are at least some distance appart each other (poisson sampling)
            return points.Except(SelfAndImmediateNeighbors)
                         .Take(Samples)
                         .OrderBy(v => (v.X + r) + (v.Y + r) * rs + (v.Z + r) * (rs * rs)) //improve cache coherency
                         .ToArray();
        }
    }
}
