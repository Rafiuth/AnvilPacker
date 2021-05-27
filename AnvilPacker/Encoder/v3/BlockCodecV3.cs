using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v3
{
    // Brotli based
    public class BlockCodecV3 : BlockCodec
    {
        public int WindowSize = 22;     // Log2 window size in bytes. Range [10..24]
        public int Quality = 8;         // Effort, 0 = no compression/fastest, 11 = best/slowest

        public BlockCodecV3(RegionBuffer region) : base(region)
        {
        }

        public override void Encode(DataWriter stream, CodecProgressListener progress = null)
        {
            using var encStream = Compressors.NewBrotliEncoder(stream, true, Quality, WindowSize);
            bool _8BitPalette = Region.Palette.Count <= 256;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(Region)) {
                for (int z = 0; z < 16; z++) {
                    for (int x = 0; x < 16; x++) {
                        int id = chunk.GetBlockIdFast(x, y, z);
                        //        Wizardry   ImpCity     Default1
                        //u8    :  ----       ----       2259,387KB
                        //u16 BE: 874,429KB  1030,351KB  2573,251KB
                        //u16 LE: 871,696KB  1027,744KB  2573,284KB
                        //VarInt: 784,508KB  943,842KB   2272,991KB

                        if (_8BitPalette) {
                            encStream.WriteByte(id);
                        } else {
                            encStream.WriteVarUInt(id);
                        }
                    }
                }
                progress?.Advance(256);
            }
            progress?.Finish();
        }

        public override void Decode(DataReader stream, CodecProgressListener progress = null)
        {
            using var decStream = Compressors.NewBrotliDecoder(stream.AsStream(), true);
            bool _8BitPalette = Region.Palette.Count <= 256;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(Region)) {
                for (int z = 0; z < 16; z++) {
                    for (int x = 0; x < 16; x++) {
                        int id = _8BitPalette ? decStream.ReadByte() 
                                              : decStream.ReadVarUInt();
                        chunk.SetBlockId(x, y, z, (BlockId)id);
                    }
                }
                progress?.Advance(256);
            }
            progress?.Finish();
        }

        public override void WriteSettings(DataWriter stream)
        {
            stream.WriteByte(0); //version
        }
        public override void ReadSettings(DataReader stream)
        {
            Ensure.That(stream.ReadByte() == 0, "Unsupported codec version");
        }
    }
}