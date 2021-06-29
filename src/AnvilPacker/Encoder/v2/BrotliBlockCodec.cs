using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v2
{
    //Based on Brotli
    public class BrotliBlockCodec : BlockCodec
    {
        public int WindowSize = 22;     // Log2 window size in bytes. Range [10..24]
        public int Quality = 6;         // Effort, 0 = no compression/fastest, 11 = best/slowest

        //brotli, win size=22
        //Q=5   2235,548KB 1.4s
        //Q=6   2225,885KB 1.8s
        //Q=7   2211,789KB 3s
        //Q=8   2193,927KB 4.6s
        //Q=9   2171,781KB 7s
        //Q=10  1829,650KB 55s
        //Q=11  1713,651KB 149s

        //zstd:
        //L=12  2234,582KB  1.8s
        //L=14  2153,230KB  4s
        //L=16  1930,162KB  9s
        //L=19  1809,353KB  16s
        //L=22  1807,023KB  41s

        public BrotliBlockCodec(RegionBuffer region) : base(region)
        {
        }

        public override void Encode(DataWriter stream, CodecProgressListener progress = null)
        {
            using var encStream = Compressors.NewBrotliEncoder(stream, true, Quality, WindowSize);
            bool _8BitPalette = Region.Palette.Count <= 256;

            foreach (var chunk in ChunkIterator.Create(Region)) {
                for (int i = 0; i < 4096; i++) {
                    int id = chunk.Blocks[i];

                    //        Wizardry   ImpCity     Default1    ItrOrder
                    //u8    :  ----       ----       2259,387KB  Layered
                    //u16 BE: 874,429KB  1030,351KB  2573,251KB  Layered
                    //u16 LE: 871,696KB  1027,744KB  2573,284KB  Layered
                    //VarInt: 784,508KB  943,842KB   2272,991KB  Layered
                    //adaptv: 721,282KB  944,802KB   2171,437KB  Chunk
                    //Q=9, W=22
                    if (_8BitPalette) {
                        encStream.WriteByte(id);
                    } else {
                        encStream.WriteVarUInt(id);
                    }
                }
                progress?.Advance(4096);
            }
            progress?.Finish();
        }

        public override void Decode(DataReader stream, CodecProgressListener progress = null)
        {
            using var decStream = Compressors.NewBrotliDecoder(stream.AsStream(), true);
            bool _8BitPalette = Region.Palette.Count <= 256;

            foreach (var chunk in ChunkIterator.Create(Region)) {
                for (int i = 0; i < 4096; i++) {
                    int id = _8BitPalette ? decStream.ReadByte()
                                          : decStream.ReadVarUInt();
                    chunk.Blocks[i] = (BlockId)id;
                }
                progress?.Advance(4096);
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