using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    using static ArithmCoderConsts;

    public static class EncoderUtils
    {
        public static void WriteVLC(this BitWriter bw, int val)
        {
            int len = BitOperations.Log2((uint)val);

            int rangeMin  = len == 0 ? 0 : (1 << len);
            int valueBits = len == 0 ? 1 : len;

            bw.WriteBits(~0, len);
            bw.WriteBit(0);
            bw.WriteBits(val - rangeMin, valueBits);
        }
        public static int ReadVLC(this BitReader br)
        {
            int len = 0;
            while (br.ReadBool()) {
                len++;
            }
            int rangeMin  = len == 0 ? 0 : (1 << len);
            int valueBits = len == 0 ? 1 : len;

            return rangeMin + br.ReadBits(valueBits);
        }

        public static void WriteVarInt(this DataWriter dw, int val)
        {
            while (val != 0) {
                int b = val & 0x7F;
                if ((val & ~0x7F) != 0) {
                    b |= 0x80;
                }
                dw.WriteByte((byte)b);
                val >>= 7;
            }
        }
        public static int ReadVarInt(this DataReader dr)
        {
            int val = 0;
            int shift = 0;
            while (true) {
                byte b = dr.ReadByte();
                val |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;

                shift += 7;
            }
            return val;
        }

        public static int VarIntSize(int val)
        {
            return 1 + BitOperations.Log2((uint)val) / 7;
        }
        public static int VarIntSize(ushort val) => VarIntSize((int)val);
    }

    public class NzContext
    {
        private const int bits = 16; //max value bits

        public BitChance Zero;
        public BitChance Sign;
        public BitChance[] Exp = new BitChance[(bits - 1) * 2];
        public BitChance[] Mant = new BitChance[bits];

        private static readonly ushort[] DefaultExpChances = new ushort[17] {
            1000, 1200, 1500, 1750, 2000, 2300, 2800, 2400, 2300,
            2048, 2048, 2048, 2048, 2048, 2048, 2048, 2048
        };
        private static readonly ushort[] DefaultMantChances = new ushort[18] {
            1900, 1850, 1800, 1750, 1650, 1600, 1600, 2048, 2048,
            2048, 2048, 2048, 2048, 2048, 2048, 2048, 2048, 2048
        };

        public NzContext()
        {
            static BitChance R(int x)
            {
                return new BitChance() {
                    Value = (ushort)(x * K / 4096)
                };
            }
            Zero = R(1000);
            Sign = R(2048);

            for (int i = 0; i < bits - 1; i++) {
                Exp[i * 2 + 0] = R(DefaultExpChances[i]);
                Exp[i * 2 + 1] = R(DefaultExpChances[i]);
            }
            for (int i = 0; i < bits; i++) {
                Mant[i] = R(DefaultMantChances[i]);
            }
        }

        public BitChance ExpProb(int i, bool sign)
            => Exp[(i << 1) + (sign ? 1 : 0)];

        public void Write(ArithmEncoder ac, int value, int min, int max)
        {
            Debug.Assert(min <= max);
            Debug.Assert(value >= min);
            Debug.Assert(value <= max);

            // avoid doing anything if the value is already known
            if (min == max) return;

            if (value == 0) { // value is zero
                Write(ac, 1, Zero);
                return;
            }
            if (min <= 0 && max >= 0) {
                // only output zero bit if value could also have been zero
                Write(ac, 0, Zero);
            }

            bool sign = value > 0;
            if (min < 0 && max > 0) {
                // only output sign bit if value can be both pos and neg
                Write(ac, sign, Sign);
            }
            if (sign) min = 1;
            if (!sign) max = -1;

            int a = Abs(value);
            int e = Log2(a);
            int amin = 1;
            int amax = sign ? Abs(max) : Abs(min);

            int emax = Log2(amax);
            int i = 0;

            for (; i < emax; i++) {
                // if exponent >i is impossible, we are done
                //if ((1 << (i + 1)) > amax) break;
                Debug.Assert(!((1 << (i + 1)) > amax));

                // if exponent i is possible, output the exponent bit
                Write(ac, i == e, ExpProb(i, sign));
                if (i == e) break;
            }

            int have = (1 << e);
            int left = have - 1;
            for (int pos = e; pos > 0;) {
                int bit = 1;
                left ^= (1 << (--pos));
                int minabs1 = have | (1 << pos);

                int maxabs0 = have | left;
                if (minabs1 > amax) { // 1-bit is impossible
                    bit = 0;
                } else if (maxabs0 >= amin) { // 0-bit and 1-bit are both possible
                    bit = (a >> pos) & 1;
                    Write(ac, bit, Mant[pos]);
                }
                have |= (bit << pos);
            }
        }
        
        public int Read(ArithmDecoder ac, int min, int max)
        {
            Debug.Assert(min <= max);
            if (min == max) return min;

            bool canBeZero = min <= 0 && max >= 0;
            if (canBeZero && Read(ac, Zero)) return 0;

            bool sign = min >= 0 || (max > 0 && Read(ac, Sign));

            int amin = 1;
            int amax = sign ? Abs(max) : Abs(min);

            int emax = Log2(amax);
            int e = 0;

            for (; e < emax; e++) {
                // if exponent >e is impossible, we are done
                // actually that cannot happen
                //if ((1 << (e+1)) > amax) break;
                if (Read(ac, ExpProb(e, sign))) break;
            }

            int have = (1 << e);
            int left = have - 1;
            for (int pos = e; pos > 0;) {
                left >>= 1; pos--;
                int minabs1 = have | (1 << pos);
                int maxabs0 = have | left;
                if (minabs1 > amax) { // 1-bit is impossible
                                      //bit = 0;
                    continue;
                } else if (maxabs0 >= amin) { // 0-bit and 1-bit are both possible
                                              //bit = coder.read(BIT_MANT,pos);
                    if (Read(ac, Mant[pos])) have = minabs1;
                } // else 0-bit is impossible, so bit stays 1
                else have = minabs1;
            }
            return (sign ? have : -have);
        }

        private void Write(ArithmEncoder ac, bool bit, BitChance prob)
        {
            ac.Write(bit, prob.Value);
            prob.Update(bit);
        }
        private void Write(ArithmEncoder ac, int bit, BitChance prob)
        {
            Write(ac, bit != 0, prob);
        }
        private bool Read(ArithmDecoder ac, BitChance prob)
        {
            bool bit = ac.ReadBool(prob.Value);
            prob.Update(bit);
            return bit;
        }

        private static int Abs(int x) => Maths.Abs(x);
        private static int Log2(int x) => BitOperations.Log2((uint)x);

        public override string ToString()
        {
            var expM = string.Join(" ", Exp.Where((b, i) => i % 2 == 0));
            var expP = string.Join(" ", Exp.Where((b, i) => i % 2 == 1));
            var mant = string.Join(" ", Mant.AsEnumerable());
            return $"Z: {Zero} S: {Sign},\nExp-1: [{expM}]\nExp+1: [{expP}]\nMant: [{mant}]"
                        .Replace("%", ""); //improve readability
        }
    }

    public class BitChance //TODO: this should be a struct
    {
        public ushort Value;

        public void Update(bool bit)
        {
            Value = (ushort)Adapt(bit, Value, 1.0 / 64);
        }
        //https://fgiesen.wordpress.com/2015/05/26/models-for-adaptive-arithmetic-coding/
        private static int Adapt(bool bit, int prob, double factor)
        {
            if (bit) {
                return Maths.Max(1, (int)(prob * (1 - factor)));
            } else {
                return Maths.Min(K - 1, (int)(prob * (1 - factor) + factor * K));
            }
        }

        //private static readonly double[] AdaptRates = { 0.005, 0.015, 0.025, 0.05 };

        //public void Update(bool bit)
        //{
        //    for (int i = 0; i < N; i++) {
        //        float newScore = FastLog2(65535f / prob[i]);
        //        score[i] = (score[i] * 15 + newScore) * (1f / 16);
        //        prob[i] = Adapt(bit, prob[i], AdaptRates[i]);
        //    }
        //
        //    for (int i = 0; i < N; i++) {
        //        if (score[i] < score[best]) {
        //            best = (byte)i;
        //        }
        //    }
        //}

        //private static float FastLog2(float x)
        //{
        //    //https://github.com/romeric/fastapprox/blob/master/fastapprox/src/fastlog.h
        //    float y = BitConverter.SingleToInt32Bits(x);
        //    y *= 1.1920928955078125e-7f;
        //    return y - 126.94269504f;
        //}

        public override string ToString() => $"{Value * 100 / K}%";
    }
}
