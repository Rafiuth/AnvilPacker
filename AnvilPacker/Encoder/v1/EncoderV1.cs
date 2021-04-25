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
    //generated using neighbor blocks. Blocks are Move-to-Front transformed on the context palette then encoded using arithmetic coding.
    //Benchmarks:
    //LZMA2: 1594KB, 16^3 chunks, 1 byte per block
    //BZip2: 1537KB, 16^3 chunks, 1 byte per block
    //APv1:  1407KB, 256^3 chunks, 2^13 contexts
    //paq8:  1225KB, 16^3 chunks, 1 byte per block (very slow, -5 took ~20min)
    public class EncoderV1
    {
        public const int MAX_CU_SIZE = 256; //must be a power of 2
        public const int CTX_BITS = 13;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        private RegionSplitter _splitter;
        private Dictionary<BlockState, int> _regionPalette = new();
        private int _predAccuracySum, _predAccuracyDiv, _unitOverhead, _blockCount;

        public EncoderV1(RegionBuffer region)
        {
            _splitter = new RegionSplitter(region, MAX_CU_SIZE);
        }

        public void Encode(DataWriter stream)
        {
            var buf = new MemoryDataWriter(1024 * 1024 * 4);
            EncodeUnits(buf);

            WriteHeader(stream);
            stream.WriteBytes(buf.BufferSpan);

            double bitsPerBlock = (buf.Position - _unitOverhead) * 8.0 / _blockCount;

            _logger.Debug($"Stats for region {_splitter.Region.X},{_splitter.Region.Z}");
            _logger.Debug($" CtxBits: {CTX_BITS}");
            _logger.Debug($" PredAccuracy: {_predAccuracySum * 100.0 / _predAccuracyDiv:0.0}%");
            _logger.Debug($" BitsPerBlock: {bitsPerBlock:0.000}");
            _logger.Debug($" Palette: {_regionPalette.Count} blocks");
            _logger.Debug($" Blocks: {(buf.Position - _unitOverhead) / 1024.0:0.000}KB");
            _logger.Debug($" Overhd: {_unitOverhead / 1024.0:0.000}KB");
            _logger.Debug($" Total: {stream.Position / 1024.0:0.000}KB");
        }

        private void EncodeUnits(DataWriter stream)
        {
            var headerBuf = new MemoryDataWriter(1024 * 32);
            var dataBuf = new MemoryDataWriter(1024 * 128);

            var contexts = new Context[1 << CTX_BITS];
            foreach (var unit in _splitter.StreamUnits()) {
                MergeUnitPalette(unit.Palette);
                
                _logger.Trace($"Encoding unit {unit.Pos} (region {_splitter.Region.X},{_splitter.Region.Z})");

                //new Transforms.HiddenBlockRemovalTransform().Apply(unit);

                ResizePalettes(contexts, unit);
                Vec3i[] neighbors = {
                    new(-1, 0, 0),
                    new(0, -1, 0),
                    new(0, 0, -1),
                };
                EncodeBlocks(unit, contexts, neighbors, dataBuf);
                WriteUnitHeader(unit, contexts, neighbors, headerBuf);

                stream.WriteBytes(headerBuf.BufferSpan);
                stream.WriteBytes(dataBuf.BufferSpan);

                _unitOverhead += (int)headerBuf.Position;
                headerBuf.Clear();
                dataBuf.Clear();

                _blockCount += unit.Size * unit.Size * unit.Size;

                /*if (false) {
                    int rw = _splitter.Region.Width * 16;
                    int rd = _splitter.Region.Depth * 16;

                    int index = (unit.Pos.X / unit.Size) + (unit.Pos.Z / unit.Size) * rw;
                    int progress = index * 100 / (rw * rd);
                }*/
            }
        }

        private void WriteUnitHeader(CodingUnit unit, Context[] contexts, Vec3i[] neighbors, DataWriter dw)
        {
            var (ux, uy, uz) = unit.Pos;
            dw.WriteVarInt(ux);
            dw.WriteVarInt(uy);
            dw.WriteVarInt(uz);
            dw.WriteVarInt(Maths.Log2(unit.Size));

            dw.WriteByte(neighbors.Length);
            foreach (var (nx, ny, nz) in neighbors) {
                int packed = (nx & 3) << 0 | // -3..0, two complement
                             (ny & 7) << 2 | // -4..3
                             (nz & 3) << 5;  // -3..0, two complement
                dw.WriteByte(packed);
            }
            dw.WriteVarInt(unit.Palette.Length);
            foreach (var block in unit.Palette) {
                dw.WriteVarInt(_regionPalette[block]);
            }

            dw.WriteByte(CTX_BITS);
        }

        private unsafe void EncodeBlocks(CodingUnit unit, Context[] contexts, Vec3i[] neighbors, DataWriter dw)
        {
            Debug.Assert(neighbors.Length <= ContextKey.MAX_SAMPLES);

            var ac = new ArithmEncoder(dw);
            int size = unit.Size;

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
                                key.s[i] = unit.GetBlock(nx, ny, nz);
                            }
                        }
                        var ctx = contexts[key.GetSlot(CTX_BITS)];

                        var id = unit.GetBlock(x, y, z);

                        int delta = ctx.PredictForward(id);
                        ctx.Nz.Write(ac, delta, 0, ctx.Palette.Length - 1);

                        _predAccuracySum += delta == 0 ? 1 : 0;
                    }
                }
            }
            _predAccuracyDiv += size * size * size;

            ac.Flush();
        }
        private void WriteHeader(DataWriter dw)
        {
            dw.WriteUShortBE(1); //data version
            WriteGlobalPalette(dw, _regionPalette.Keys);
        }

        private void WriteGlobalPalette(DataWriter dw, ICollection<BlockState> palette)
        {
            dw.WriteVarInt(palette.Count);
            foreach (var block in palette) {
                var name = Encoding.UTF8.GetBytes(block.ToString());
                dw.WriteVarInt(name.Length);
                dw.WriteBytes(name);

                dw.WriteVarInt((int)block.Attributes);
            }
        }

        private void MergeUnitPalette(BlockState[] palette)
        {
            foreach (var block in palette) {
                _regionPalette.TryAdd(block, _regionPalette.Count);
            }
        }
        private void ResizePalettes(Context[] contexts, CodingUnit unit)
        {
            for (int i = 0; i < contexts.Length; i++) {
                var ctx = contexts[i] ??= new Context();
                
                ctx.Palette ??= Array.Empty<ushort>();
                
                int oldLen = ctx.Palette.Length;
                int newLen = unit.Palette.Length;
                if (oldLen < newLen) {
                    Array.Resize(ref ctx.Palette, newLen);
                    for (int j = oldLen; j < newLen; j++) {
                        ctx.Palette[j] = (ushort)j;
                    }
                }
            }
        }

    }
}
