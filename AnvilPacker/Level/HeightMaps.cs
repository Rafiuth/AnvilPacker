#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    using Map = KeyValuePair<HeightMapType, short[]>;
    public class HeightMaps : IEnumerable<Map>
    {
        private List<Map> _values = new();

        public short[]? Get(HeightMapType type, bool create = false)
        {
            foreach (var (k, v) in _values) {
                if (k.Equals(type)) {
                    return v;
                }
            }
            if (create) {
                var map = new short[16 * 16];
                _values.Add(new(type, map));
                return map;
            }
            return null;
        }

        public static void Calculate(Chunk chunk, HeightMapType type, short[] heights)
        {
            //bitmap where bit[i] indicates whether heights[i] contains the final value.
            var populated = new BitSet(16 * 16);
            heights.Fill((short)(chunk.MinSectionY * 16));

            for (int sy = chunk.MaxSectionY; sy >= chunk.MinSectionY; sy--) {
                var section = chunk.GetSection(sy);
                if (section == null) continue;

                for (int y = 15; y >= 0; y--) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            var block = section.GetBlock(x, y, z);
                            int index = x + z * 16;
                            if (type.IsOpaque(block) && !populated[index]) {
                                heights[index] = (short)(sy * 16 + y + 1);
                                populated[index] = true;
                            }
                        }
                    }
                }
                if (populated.All(true)) break;
            }
        }

        public IEnumerator<Map> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class HeightMapType
    {
        private static readonly List<HeightMapType> _knownTypes = new();

        public static readonly HeightMapType 
            WorldSurfaceWG         = new("WORLD_SURFACE_WG", b => b.Material != BlockMaterial.Air),
            WorldSurface           = new("WORLD_SURFACE",    b => b.Material != BlockMaterial.Air),
            OceanFloorWG           = new("OCEAN_FLOOR_WG",   b => b.Material.BlocksMotion),
            OceanFloor             = new("OCEAN_FLOOR",      b => b.Material.BlocksMotion),
            MotionBlocking         = new("MOTION_BLOCKING",  b => b.Material.BlocksMotion || HasFluid(b)),
            MotionBlockingNoLeaves = new("MOTION_BLOCKING_NO_LEAVES", b => (b.Material.BlocksMotion || HasFluid(b)) && b.Material != BlockMaterial.Leaves);

        public string Name { get; }
        public Predicate<BlockState> IsOpaque { get; }
        public bool KeepAfterWorldGen { get; }

        private HeightMapType(string name, Predicate<BlockState> isOpaque, bool isKnown = true)
        {
            Name = name;
            IsOpaque = isOpaque;
            KeepAfterWorldGen = !name.EndsWith("_WG");
            if (isKnown) {
                _knownTypes.Add(this);
            }
        }

        private static bool HasFluid(BlockState block)
        {
            //TODO: update DataExtractor and get fluid states
            return block.Material.IsLiquid ||
                   block.Material == BlockMaterial.UnderwaterPlant ||
                   block.Material == BlockMaterial.ReplaceableUnderwaterPlant;
        }

        public static HeightMapType ForName(string name)
        {
            return _knownTypes.Find(v => v.Name == name) ?? 
                   new HeightMapType(name, MotionBlocking.IsOpaque, false);
        }

        public override bool Equals(object? obj)
        {
            return obj is HeightMapType other && other.Name == Name;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
        public override string ToString() => Name;
    }
}