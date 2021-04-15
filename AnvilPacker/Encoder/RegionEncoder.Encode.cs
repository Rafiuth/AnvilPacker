using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Microsoft.Collections.Extensions;

namespace AnvilPacker.Encoder
{
    public partial class RegionEncoder
    {
        private int _predAccuracySum;
        private int _predAccuracyDiv;
        private int _unitHdrOverhead;

        public void Encode()
        {
            var unitBuffer = new MemoryDataWriter(1024 * 1024 * 4);
            var headerBuffer = new MemoryDataWriter(1024 * 32);

            EncodeUnits(unitBuffer);
            WriteHeader(headerBuffer);

            Console.WriteLine($"CuSize: {CU_SIZE} CtxBits: {CTX_BITS}");
            Console.WriteLine($"PredAccuracy: {_predAccuracySum * 100.0 / _predAccuracyDiv:0.0}%");
            Console.WriteLine($"Header: {headerBuffer.Position / 1024.0:0.000}KB");
            Console.WriteLine($"Blocks: {unitBuffer.Position / 1024.0:0.000}KB");
            Console.WriteLine($"Overhd: {_unitHdrOverhead / 1024.0:0.000}KB");
        }

        private void EncodeUnits(DataWriter dw)
        {
            for (int y = 0; y < _height; y++) {
                for (int z = 0; z < _depth; z++) {
                    for (int x = 0; x < _width; x++) {
                        int index = GetUnitIndex(x, y, z);
                        var pos = new Vec3i(x, y, z) * CU_SIZE;
                        var unit = CreateUnit(pos, CU_SIZE);
                        if (unit != null) {
                            _hasUnit[index] = true;
                            AnalyzeUnit(unit);
                            EncodeUnit(dw, unit);
                        }
                    }
                    Console.WriteLine($"Encoding region... {(y*_depth+z) * 100 / (_height*_depth)}%    ");
                }
            }
        }

        private void EncodeUnit(DataWriter dw, CodingUnit unit)
        {
            long startPos = dw.Position;
            dw.WriteByte(unit.ContextNeighbors.Length);
            foreach (var pos in unit.ContextNeighbors) {
                int packed = (pos.X & 3) << 0 | // -3..0, two complement
                             (pos.Y & 7) << 2 | // -4..3
                             (pos.Z & 3) << 5;  // -3..0, two complement
                dw.WriteByte(packed);
            }

            dw.WriteByte(CTX_BITS);

            var nz = new NzContext();
            var ac = new ArithmEncoder(dw);
            foreach (var ctx in unit.Contexts) {
                var ctxPalette = ctx.Palette;

                //dw.WriteVarInt(ctxPalette.Length);
                nz.Write(ac, ctx.Palette.Length, 0, unit.Palette.Length - 1);
                int min = unit.Palette.Min();
                int max = unit.Palette.Max();
                foreach (var entry in ctxPalette) {
                    nz.Write(ac, entry, min, max);
                    //dw.WriteVarInt(entry);
                }
            }
            ac.Flush();
            long endPos = dw.Position;
            _unitHdrOverhead += (int)(endPos - startPos);

            ac = new ArithmEncoder(dw);
            EncodeBlocks(ac, unit);
            ac.Flush();
        }

        private void EncodeBlocks(ArithmEncoder ac, CodingUnit unit)
        {
            var size = unit.Size;
            var blocks = unit.Blocks;
            var blockContexts = unit.BlockContexts;
            var contexts = unit.Contexts;

            for (int y = 0; y < size; y++) {
                for (int z = 0; z < size; z++) {
                    for (int x = 0; x < size; x++) {
                        int idx = unit.GetIndex(x, y, z);
                        var id = blocks[idx];
                        var ctx = contexts[blockContexts[idx]];

                        int delta = ctx.PredictForward(id);

                        ctx.Nz.Write(ac, delta, 0, ctx.Palette.Length - 1);

                        _predAccuracySum += delta == 0 ? 1 : 0;
                        _predAccuracyDiv++;
                    }
                }
            }
        }

        private void WriteHeader(DataWriter dw)
        {
            dw.WriteUShort(0); //data version
            WriteGlobalPalette(dw, _invPalette);

            var bw = new BitWriter(dw);
            WriteUnitBitmap(bw);
            bw.Flush();
        }

        private void WriteGlobalPalette(DataWriter dw, List<IBlockState> palette)
        {
            dw.WriteVarInt(palette.Count);
            for (int i = 0; i < palette.Count; i++) {
                var block = palette[i];
                var name = Encoding.UTF8.GetBytes(block.ToString());
                dw.WriteVarInt(name.Length);
                dw.WriteBytes(name);

                dw.WriteVarInt(_categories[i]);
            }
        }

        private void WriteUnitBitmap(BitWriter bw)
        {
            //This is a simple "auto trigger" RLE encoding
            var bitmap = _hasUnit;

            int literals = 0;
            int i = 0;
            while (i < bitmap.Length) {
                //calculate run length
                //TODO: use something like FindNextSetBit()
                int j = i + 1;
                var val = bitmap[i];
                while (j < bitmap.Length && bitmap[j] == val) j++;

                int len = j - i;
                bool isRun = len >= 2;

                if (isRun && literals > 0) {
                    Encode(i - literals, literals, true); //encode pending literals
                    literals = 0;
                }
                if (isRun) {
                    Encode(i, len, false);
                } else {
                    literals += len; //add to pending literals
                }
                i = j;
            }
            if (literals > 0) {
                //encode last pending literals
                Encode(bitmap.Length - 1 - literals, literals, true);
            }

            void Encode(int start, int count, bool isLiteral)
            {
                int numLiterals = isLiteral ? count : 2;
                for (int i = 0; i < numLiterals; i++) {
                    bw.WriteBit(bitmap[start + i]);
                }
                if (!isLiteral) {
                    bw.WriteVLC(count - 2);
                }
            }
        }
    }
}
