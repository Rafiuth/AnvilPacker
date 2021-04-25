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

        public readonly RegionBuffer Region;

        /// <summary> Unit size, in all 3 axis. </summary>
        public readonly int Size;

        public RegionSplitter(RegionBuffer region, int size)
        {
            if (size <= 0) {
                throw new ArgumentException("Unit size must be greater than 0.");
            }
            Region = region;
            Size = size;
        }

        public IEnumerable<CodingUnit> StreamUnits()
        {
            int width = Maths.CeilDiv(Region.Width * 16, Size);
            int depth = Maths.CeilDiv(Region.Depth * 16, Size);

            for (int z = 0; z < depth; z++) {
                for (int x = 0; x < width; x++) {
                    var (minY, maxY) = GetChunkYBounds(x, z);
                    for (int y = minY; y < maxY; y++) {
                        var unit = CreateUnit(new Vec3i(x, y, z) * Size);
                        if (unit != null) {
                            yield return unit;
                        }
                    }
                }
            }
        }
        private (int Min, int Max) GetChunkYBounds(int ux, int uz)
        {
            int cx1 = ux * Size / 16;
            int cz1 = uz * Size / 16;
            int cx2 = Maths.CeilDiv((ux + 1) * Size, 16);
            int cz2 = Maths.CeilDiv((uz + 1) * Size, 16);

            int minY = 1024, maxY = -1024;
            for (int z = cz1; z < cz2; z++) {
                for (int x = cx1; x < cx2; x++) {
                    var chunk = Region.GetChunk(x, z);
                    if (chunk == null) continue;

                    for (int i = 0; i < chunk.Sections.Length; i++) {
                        if (chunk.Sections[i] != null) {
                            minY = Math.Min(minY, i);
                            maxY = Math.Max(maxY, i);
                        }
                    }
                }
            }
            minY = minY * 16 / Size;
            maxY = Maths.CeilDiv((maxY + 1) * 16, Size);
            return (minY, maxY);
        }

        public CodingUnit CreateUnit(Vec3i pos)
        {
            var unit = new CodingUnit(pos, Size);
            var palette = new Palette();

            foreach (var chunk in Region.GetChunks(pos, pos + Size)) {

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


        private class Palette
        {
            private DictionarySlim<int, ushort> _entries = new(256);
            private int[] _freqs = new int[256];

            public ushort Get(BlockState block)
            {
                if (_entries.TryGetValue(block.Id, out ushort id)) {
                    _freqs[id]++;
                    return id;
                }
                if (_entries.Count > MAX_PALETTE_ID) {
                    //unrealistic for now, vanilla 1.16 have ~18K block states.
                    throw new NotSupportedException("Unit palette cannot have more than 64K entries.");
                }
                id = (ushort)_entries.Count;
                _entries.GetOrAddValueRef(block.Id) = id;
                if (_freqs.Length < _entries.Count) {
                    Array.Resize(ref _freqs, _entries.Count * 2);
                }
                return id;
            }

            public BlockState[] ToArray()
            {
                return _entries.OrderByDescending(v => _freqs[v.Value])
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
