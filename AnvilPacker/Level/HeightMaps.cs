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
        public int Count => _values.Count;

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

        public IEnumerator<Map> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    /// <summary> Provides methods for computing heightmaps for chunks in a region. </summary>
    public class HeightMapComputer
    {
        private RegionBuffer _region;
        private bool[] _isOpaque;
        private BitSet _populated;
        private HeightMapType _type;

        public HeightMapComputer(RegionBuffer region, HeightMapType type)
        {
            _region = region;
            _populated = new BitSet(16 * 16);
            _type = type;

            var palette = region.Palette;
            _isOpaque = new bool[palette.Count];
            foreach (var (block, id) in palette.BlocksAndIds()) {
                _isOpaque[id] = type.IsOpaque(block);
            }
        }
        public void Compute(Chunk chunk)
        {
            var heightmap = chunk.HeightMaps.Get(_type, true);
            Compute(chunk, heightmap!);
        }
        public void Compute(Chunk chunk, short[] heightmap)
        {
            Ensure.That(chunk.Palette == _region.Palette, "Chunk not in region");

            var populated = _populated;
            populated.Clear();

            int numPopulated = 0;
            var (minSy, maxSy) = chunk.GetActualYExtents();

            int defaultHeight = minSy * 16;
            if (_type == HeightMapType.Legacy) {
                defaultHeight = maxSy * 16 + 15;
            }
            heightmap.Fill((short)defaultHeight);

            for (int sy = maxSy; sy >= minSy; sy--) {
                var section = chunk.GetSection(sy);
                if (section == null) continue;

                for (int y = 15; y >= 0; y--) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            var block = section.GetBlockId(x, y, z);
                            int index = x + z * 16;
                            if (_isOpaque[block] && !populated[index]) {
                                heightmap[index] = (short)(sy * 16 + y + 1);
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

    public class HeightMapType
    {
        public static readonly List<HeightMapType> KnownTypes = new();

        public static readonly HeightMapType 
            Legacy                 = new("LEGACY", b => b.Opacity != 0),
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
                KnownTypes.Add(this);
            }
        }

        private static bool HasFluid(BlockState block)
        {
            return block.HasAttribs(BlockAttributes.IsImmerse);
        }

        public static HeightMapType ForName(string name)
        {
            return KnownTypes.Find(v => v.Name == name) ?? 
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