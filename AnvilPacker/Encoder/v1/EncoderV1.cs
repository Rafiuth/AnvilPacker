using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Microsoft.Collections.Extensions;
using NLog;

namespace AnvilPacker.Encoder.v1
{
    //Version 1 uses a fixed size context table. The context is selected based on a hash
    //generated using neighbor blocks. Move-To-Front transform is used along arithmetic coding.
    //Benchmarks:
    //LZMA2: 1594KB, 16^3 chunks, 1 byte per block
    //BZip2: 1537KB, 16^3 chunks, 1 byte per block
    //APv1:  1393KB, 256^3 chunks, 2^13 contexts

    //This used to encode the palette for each context, but it adds too much overhead.
    //Maybe we could transmit just a subset, but I have no idea how to fill the rest of the contexts.
    //A O(n^3) loop would be simply too slow for big palettes.
    public class EncoderV1
    {
        public const int CU_SIZE = 256; //must be a power of 2
        public const int CTX_BITS = 13;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        private RegionSplitter _splitter;
        private int _predAccuracySum, _predAccuracyDiv, _unitOverhead, _unitCount;

        public EncoderV1(RegionBuffer region)
        {
            _splitter = new RegionSplitter(region, CU_SIZE);
        }

        public void Encode(DataWriter stream)
        {
            var buf = new MemoryDataWriter(1024 * 1024 * 4);
            EncodeUnits(buf);

            WriteHeader(stream);
            stream.WriteBytes(buf.BufferSpan);

            long totalBlocks = (CU_SIZE * CU_SIZE * CU_SIZE) * (long)_unitCount;
            double bitsPerBlock = (buf.Position - _unitOverhead) * 8.0 / totalBlocks;

            _logger.Debug($"Stats for region {_splitter.Region.X},{_splitter.Region.Z}");
            _logger.Debug($" CuSize: {CU_SIZE} CtxBits: {CTX_BITS} Units: {_unitCount}");
            _logger.Debug($" PredAccuracy: {_predAccuracySum * 100.0 / _predAccuracyDiv:0.0}%");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}");
            _logger.Debug($" Palette: {_splitter.Palette.Count} blocks");
            _logger.Debug($" Blocks: {(buf.Position - _unitOverhead) / 1024.0:0.000}KB");
            _logger.Debug($" Overhd: {_unitOverhead / 1024.0:0.000}KB");
            _logger.Debug($" Total: {stream.Position / 1024.0:0.000}KB");
        }

        private void EncodeUnits(DataWriter stream)
        {
            var headerBuf = new MemoryDataWriter(1024 * 32);
            var dataBuf = new MemoryDataWriter(1024 * 128);

            foreach (var unit in _splitter.StreamUnits()) {
                _logger.Trace($"Encoding unit {unit.Pos} (region {_splitter.Region.X},{_splitter.Region.Z})");

                var contexts = CreateContexts(unit, CTX_BITS);
                Vec3i[] neighbors = {
                    new(-1, 0, 0),
                    new(0, -1, 0),
                    new(0, 0, -1),
                };
                //new Transforms.HiddenBlockRemovalTransform().Apply(_splitter, unit);
                EncodeBlocks(unit, contexts, neighbors, dataBuf);
                EncodeUnitHeader(unit, contexts, neighbors, headerBuf);

                stream.WriteBytes(headerBuf.BufferSpan);
                stream.WriteBytes(dataBuf.BufferSpan);

                _unitOverhead += (int)headerBuf.Position;
                headerBuf.Clear();
                dataBuf.Clear();

                _unitCount++;

                /*if (false) {
                    int rw = _splitter.Region.Width * 16;
                    int rd = _splitter.Region.Depth * 16;

                    int index = (unit.Pos.X / unit.Size) + (unit.Pos.Z / unit.Size) * rw;
                    int progress = index * 100 / (rw * rd);
                }*/
            }
        }

        private void EncodeUnitHeader(CodingUnit unit, Context[] contexts, Vec3i[] neighbors, DataWriter dw)
        {
            dw.WriteVarInt(unit.Pos.X);
            dw.WriteVarInt(unit.Pos.Y);
            dw.WriteVarInt(unit.Pos.Z);

            dw.WriteByte(neighbors.Length);
            foreach (var pos in neighbors) {
                int packed = (pos.X & 3) << 0 | // -3..0, two complement
                             (pos.Y & 7) << 2 | // -4..3
                             (pos.Z & 3) << 5;  // -3..0, two complement
                dw.WriteByte(packed);
            }
            dw.WriteVarInt(unit.Palette.Length);
            foreach (var id in unit.Palette) {
                dw.WriteVarInt(id);
            }

            dw.WriteByte(CTX_BITS);
        }

        private unsafe void EncodeBlocks(CodingUnit unit, Context[] contexts, Vec3i[] neighbors, DataWriter dw)
        {
            Debug.Assert(neighbors.Length <= ContextKey.MAX_SAMPLES);

            var ac = new ArithmEncoder(dw);
            int size = unit.Size;
            var blocks = unit.Blocks;

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
                                key.s[i] = blocks[unit.GetIndex(nx, ny, nz)];
                            }
                        }
                        int slot = key.GetSlot(CTX_BITS);
                        var ctx = contexts[slot];

                        int idx = unit.GetIndex(x, y, z);
                        var id = blocks[idx];

                        int delta = ctx.PredictForward(id);
                        ctx.Nz.Write(ac, delta, 0, ctx.Palette.Length - 1);

                        _predAccuracySum += delta == 0 ? 1 : 0;
                    }
                }
            }
            _predAccuracyDiv += size * size * size;

            ac.Flush();
        }
        private Context[] CreateContexts(CodingUnit unit, int bits)
        {
            var contexts = new Context[1 << bits];
            for (int i = 0; i < contexts.Length; i++) {
                contexts[i] = new Context() {
                    Palette = unit.Palette.AsSpan().ToArray()
                };
            }
            return contexts;
        }

        private void WriteHeader(DataWriter dw)
        {
            dw.WriteUShort(1); //data version
            WriteGlobalPalette(dw, _splitter.InvPalette);
        }

        private void WriteGlobalPalette(DataWriter dw, List<BlockState> palette)
        {
            dw.WriteVarInt(palette.Count);
            for (int i = 0; i < palette.Count; i++) {
                var block = palette[i];
                var name = Encoding.UTF8.GetBytes(block.ToString());
                dw.WriteVarInt(name.Length);
                dw.WriteBytes(name);

                dw.WriteVarInt((int)block.Attributes);
            }
        }
    }
}
