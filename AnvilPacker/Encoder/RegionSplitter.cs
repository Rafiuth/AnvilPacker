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

        public readonly DictionarySlim<int, ushort> Palette = new(512); //block state id -> internal id
        public readonly List<BlockState> InvPalette = new(512);  //internal id -> block state id

        public RegionSplitter(RegionBuffer region, int size)
        {
            if (!Maths.IsPow2(size)) {
                throw new ArgumentException("Unit size must be a power of two.");
            }
            Region = region;
            Size = size;

            GetPaletteId(BlockState.Air); //force 0th palette entry to be air
        }

        public IEnumerable<CodingUnit> StreamUnits()
        {
            int width = (Region.Width * 16) / Size;
            int depth = (Region.Depth * 16) / Size;

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
            int cx2 = (ux + 1) * Size / 16;
            int cz2 = (uz + 1) * Size / 16;

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
            var palette = new DictionarySlim<ushort, int>();

            foreach (var chunk in Region.GetChunks(pos, pos + Size)) {

                for (int y = chunk.Y0; y < chunk.Y1; y++) {
                    for (int z = chunk.Z0; z < chunk.Z1; z++) {
                        for (int x = chunk.X0; x < chunk.X1; x++) {
                            var block = chunk.GetBlock(x, y, z);
                            ushort id = GetPaletteId(block);

                            unit.SetBlock(x - pos.X, y - pos.Y, z - pos.Z, id);
                            palette.GetOrAddValueRef(id)++;
                        }
                    }
                }
            }

            if (palette.Count == 0) {
                return null;
            }
            if (palette.Count == 1) {
                var type = palette.First().Key;
                if (GetPaletteState(type).Material == BlockMaterial.Air) {
                    return null;
                }
            }
            //ordering by frequency helps the MTF a bit
            unit.Palette = palette.OrderByDescending(v => v.Value).Select(v => v.Key).ToArray();
            
            return unit;
        }

        /// <summary> Gets or adds a block to the palette. </summary>
        public ushort GetPaletteId(BlockState block)
        {
            if (Palette.TryGetValue(block.Id, out ushort id)) {
                return id;
            }
            if (InvPalette.Count > MAX_PALETTE_ID) {
                //unrealistic for now, vanilla 1.16 contains ~18K block states.
                throw new NotSupportedException("Region palette cannot have more than 64K entries.");
            }
            id = (ushort)InvPalette.Count;
            InvPalette.Add(block);
            Palette.GetOrAddValueRef(block.Id) = id;
            return id;
        }
        public BlockState GetPaletteState(ushort id)
        {
            return InvPalette[id];
        }
    }
}
