using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Microsoft.Collections.Extensions;

namespace AnvilPacker.Encoder
{
    public unsafe partial class RegionEncoder
    {
        private const int CU_SIZE = 128; //must be a power of 2, <= 256
        private const int CTX_BITS = 9; //must be <= 16
        private const ushort MAX_PALETTE_ID = ushort.MaxValue - 1; //65535 is reserved

        internal readonly RegionBuffer _region;
        internal readonly EncoderContext _ctx;

        internal readonly int _width, _height, _depth;

        internal DictionarySlim<int, ushort> _palette = new(512); //all blocks in this region
        internal List<IBlockState> _invPalette = new(512);
        internal List<ushort> _categories = new(512);

        internal readonly BitArray _hasUnit; //unit bitmap

        public RegionEncoder(EncoderContext ctx, RegionBuffer region)
        {
            _region = region;
            _ctx = ctx;

            _width = (region.Width * 16) / CU_SIZE;
            _depth = (region.Depth * 16) / CU_SIZE;
            _height = 256 / CU_SIZE;

            _hasUnit = new BitArray(_width * _height * _depth);
        }

        private CodingUnit CreateUnit(Vec3i pos, int size)
        {
            var unit = new CodingUnit(pos, size);
            var palette = new HashSet<ushort>();

            foreach (var chunk in _region.GetChunks(pos, pos + size)) {

                for (int y = chunk.Y0; y < chunk.Y1; y++) {
                    for (int z = chunk.Z0; z < chunk.Z1; z++) {
                        for (int x = chunk.X0; x < chunk.X1; x++) {
                            var block = chunk.GetBlock(x, y, z);
                            ushort id = GetPalettedId(block);

                            unit.SetBlock(x - pos.X, y - pos.Y, z - pos.Z, id);
                            palette.Add(id);
                        }
                    }
                }
            }

            if (palette.Count == 0) {
                return null;
            }
            if (palette.Count == 1) {
                var type = palette.First();
                if (_invPalette[type].Material == BlockMaterial.Air) {
                    return null;
                }
            }
            unit.Palette = palette.ToArray();

            return unit;
        }

        private void AnalyzeUnit(CodingUnit unit)
        {
            var contexts = CreateContexts(unit, CTX_BITS);

            Vec3i[] neighbors = {
                //new(-1, 0, 0),
                new(0, -1, 0),
                new(0, 0, -1),
            };
            unit.ContextNeighbors = neighbors;
            unit.Contexts = contexts;

            int size = unit.Size;
            var blocks = unit.Blocks;
            var blockContexts = unit.BlockContexts;

            for (int y = 0; y < size; y++) {
                for (int z = 0; z < size; z++) {
                    for (int x = 0; x < size; x++) {
                        var key = new ContextKey();

                        for (int i = 0; i < neighbors.Length; i++) {
                            var rel = neighbors[i];
                            int nx = x + rel.X;
                            int ny = y + rel.Y;
                            int nz = z + rel.Z;

                            if ((uint)(nx | ny | nz) < (uint)size) {
                                var nid = blocks[unit.GetIndex(nx, ny, nz)];
                                key.s[i] = _categories[nid];
                            }
                        }
                        int slot = key.GetSlot(CTX_BITS);
                        var ctx = contexts[slot];

                        int idx = unit.GetIndex(x, y, z);
                        var id = blocks[idx];

                        blockContexts[idx] = (ushort)slot;

                        if (!ctx.BlockUsed[id]) {
                            ctx.BlockUsed[id] = true;
                            ctx.Palette[ctx.PaletteLen++] = id;
                        }
                    }
                }
            }

            foreach (var ctx in contexts) {
                Array.Resize(ref ctx.Palette, ctx.PaletteLen);
                ctx.BlockUsed = null;
            }
        }

        private Context[] CreateContexts(CodingUnit unit, int bits)
        {
            var contexts = new Context[1 << bits];
            for (int i = 0; i < contexts.Length; i++) {
                contexts[i] = new Context() {
                    BlockUsed = new BitArray(_palette.Count),
                    Palette = new ushort[unit.Palette.Length + 1]
                };
            }
            return contexts;
        }

        /// <summary> Gets or adds a block to the palette. </summary>
        internal ushort GetPalettedId(IBlockState block)
        {
            if (_palette.TryGetValue(block.Id, out ushort id)) {
                return id;
            }
            if (_invPalette.Count > MAX_PALETTE_ID) {
                //unrealistic for now, vanilla 1.16 contains ~18K block states.
                throw new NotSupportedException("Region palette cannot have more than 64K entries.");
            }
            id = (ushort)_invPalette.Count;
            _invPalette.Add(block);
            _palette.GetOrAddValueRef(block.Id) = id;
            _categories.Add(GetCategory(block));
            return id;
        }

        private ushort GetCategory(IBlockState block)
        {
            return GetPalettedId(block);
            //return (ushort)((MBlockState)block).Attributes;
            //int id = BlockMaterial.Registry[block.Material];
            //return (ushort)id;
        }

        private int GetUnitIndex(int x, int y, int z)
        {
            return y * (_width * _depth) +
                   z * _width +
                   x;
        }
    }
}
