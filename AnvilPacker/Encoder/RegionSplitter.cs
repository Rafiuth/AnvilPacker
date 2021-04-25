using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Microsoft.Collections.Extensions;

namespace AnvilPacker.Encoder
{
    public class RegionSplitter
    {
        public const ushort MAX_PALETTE_ID = ushort.MaxValue - 1; //65535 is reserved
        public const int MIN_UNIT_SIZE = 16;

        public readonly RegionBuffer Region;

        /// <summary> Max unit size, in block steps. </summary>
        public readonly int MaxSize;
        /// <summary> Whether to try minimize unit sizes. </summary>
        public readonly bool SubDivide;

        public RegionSplitter(RegionBuffer region, int maxSize, bool subDivide = false)
        {
            Ensure.That(maxSize >= MIN_UNIT_SIZE && Maths.IsPow2(maxSize), $"Max unit size must be >= {MIN_UNIT_SIZE} and a power of two.");
            Region = region;
            MaxSize = maxSize;
            SubDivide = subDivide;
        }

        public IEnumerable<CodingUnit> StreamUnits()
        {
            int regionSize = Region.Size;
            int step = MaxSize / 16;
            Ensure.That(step > 0, "MaxSize must be smaller than the region size.");

            for (int z = 0; z < regionSize; z += step) {
                for (int x = 0; x < regionSize; x += step) {
                    var (y1, y2) = GetChunkYExtents(x, z, x + step, z + step);

                    for (int y = y1; y < y2; y += step) {
                        var unit = CreateUnit(new Vec3i(x, y, z) * 16, MaxSize);

                        if (unit != null && SubDivide) {
                            foreach (var subunit in SubDivideUnit(unit)) {
                                yield return subunit;
                            }
                        } else if (unit != null) {
                            yield return unit;
                        }
                    }
                }
            }
        }
        private IEnumerable<CodingUnit> SubDivideUnit(CodingUnit unit)
        {
            throw new NotImplementedException();
        }

        public CodingUnit CreateUnit(Vec3i pos, int size)
        {
            var unit = new CodingUnit(pos, size);
            var palette = new Palette();

            foreach (var chunk in Region.GetChunks(pos, pos + size)) {

                for (int y = chunk.Y0; y < chunk.Y1; y++) {
                    for (int z = chunk.Z0; z < chunk.Z1; z++) {
                        for (int x = chunk.X0; x < chunk.X1; x++) {
                            var block = chunk.GetBlock(x, y, z);
                            ushort id = palette.Get(block);

                            unit.SetBlock(x - pos.X, y - pos.Y, z - pos.Z, id);
                        }
                    }
                }
            }

            if (palette.IsEmptyOrAllAir()) {
                return null;
            }
            unit.Palette = palette.ToArray();

            return unit;
        }
        private (int Min, int Max) GetChunkYExtents(int x1, int z1, int x2, int z2)
        {
            int y1 = int.MaxValue, y2 = int.MinValue;

            for (int z = z1; z < z2; z++) {
                for (int x = x1; x < x2; x++) {
                    var chunk = Region.GetChunk(x, z);
                    if (chunk == null) continue;

                    for (int y = chunk.MinSectionY; y < chunk.MaxSectionY; y++) {
                        if (chunk.GetSection(y) != null) {
                            y1 = Math.Min(y1, y);
                            y2 = Math.Max(y2, y);
                        }
                    }
                }
            }
            return (y1, y2 + 1);
        }

        private class Palette
        {
            private DictionarySlim<int, ushort> _entries = new(256);

            public ushort Get(BlockState block)
            {
                if (_entries.TryGetValue(block.Id, out ushort id)) {
                    return id;
                }
                Ensure.That(_entries.Count <= MAX_PALETTE_ID, "Unit palette cannot have more than 64K entries.");
                
                id = (ushort)_entries.Count;
                _entries.GetOrAddValueRef(block.Id) = id;

                return id;
            }

            public BlockState[] ToArray()
            {
                //TODO: ordering by frequency requires reindexing the block array
                return _entries.OrderBy(v => v.Value)
                               .Select(v => Block.StateRegistry[v.Key])
                               .ToArray();
            }

            public bool IsEmptyOrAllAir()
            {
                if (_entries.Count == 0) {
                    return true;
                }
                if (_entries.Count == 1) {
                    var stateId = _entries.First().Key;
                    var state = Block.StateRegistry[stateId];
                    return state.Material == BlockMaterial.Air;
                }
                return false;
            }
        }
    }
}
