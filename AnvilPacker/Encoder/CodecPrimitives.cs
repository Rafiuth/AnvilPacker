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

    public static class CodecPrimitives
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

        public static void WriteVarUInt(this DataWriter dw, int val)
        {
            while (val != 0) {
                int b = val & 0x7F;
                if ((val & ~0x7F) != 0) {
                    b |= 0x80;
                }
                dw.WriteByte((byte)b);
                val = (int)((uint)val >> 7);
            }
        }
        public static int ReadVarUInt(this DataReader dr)
        {
            int val = 0;
            int shift = 0;
            while (shift < 32) {
                byte b = dr.ReadByte();
                val |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) {
                    return val;
                }
                shift += 7;
            }
            throw new FormatException("Corrupted VarInt");
        }

        public static void WriteVarInt(this DataWriter dw, int val)
        {
            //See https://developers.google.com/protocol-buffers/docs/encoding#signed_integers
            dw.WriteVarUInt((val << 1) ^ (val >> 31));
        }
        public static int ReadVarInt(this DataReader dr)
        {
            var val = dr.ReadVarUInt();
            return (int)((uint)val >> 1) ^ -(val & 1);
        }

        public static void WriteVarULong(this DataWriter dw, long val)
        {
            while (val != 0) {
                int b = (int)(val & 0x7FL);
                if ((val & ~0x7FL) != 0) {
                    b |= 0x80;
                }
                dw.WriteByte((byte)b);
                val = (long)((ulong)val >> 7);
            }
        }
        public static long ReadVarULong(this DataReader dr)
        {
            long val = 0;
            int shift = 0;
            while (shift < 64) {
                byte b = dr.ReadByte();
                val |= (b & 0x7FL) << shift;
                if ((b & 0x80) == 0) {
                    return val;
                }
                shift += 7;
            }
            throw new FormatException("Corrupted VarLong");
        }
        public static void WriteVarLong(this DataWriter dw, long val)
        {
            //See https://developers.google.com/protocol-buffers/docs/encoding#signed_integers
            dw.WriteVarULong((val << 1) ^ (val >> 63));
        }
        public static long ReadVarLong(this DataReader dr)
        {
            var val = dr.ReadVarULong();
            return (int)((ulong)val >> 1) ^ -(val & 1);
        }

        public static int VarIntSize(long val)
        {
            return VarUIntSize((val << 1) ^ (val >> 63));
        }
        public static int VarUIntSize(long val)
        {
            return 1 + BitOperations.Log2((ulong)val) / 7;
        }

        public static void RunLengthEncode(int length, Func<int, int, bool> compare, Action<int> writeLiteral, Action<int> writeRunLen)
        {
            int literals = 0;
            int i = 0;
            while (i < length) {
                //calculate run length
                int j = i + 1;
                while (j < length && compare(i, j)) j++;

                int len = j - i;
                bool isRun = len >= 2;

                if (isRun && literals > 0) {
                    //encode pending literals
                    Encode(i - literals, literals, true);
                    literals = 0;
                }
                if (isRun) {
                    Encode(i, len, false);
                } else {
                    literals += len;
                }
                i = j;
            }
            if (literals > 0) {
                //encode last pending literals
                Encode(length - literals, literals, true);
            }
            void Encode(int start, int count, bool isLiteral)
            {
                int numLiterals = isLiteral ? count : 2;
                for (int i = 0; i < numLiterals; i++) {
                    writeLiteral(start + i);
                }
                if (!isLiteral) {
                    writeRunLen(count - 2);
                }
            }
        }
        public static void RunLengthDecode<T>(int length, Func<T> readLiteral, Func<int> readRunLen, Action<int, T> consume) where T : IEquatable<T>
        {
            T prev = default;

            for (int i = 0; i < length; ) {
                var val = readLiteral();
                consume(i, val);
                i++;

                if (i > 1 && prev.Equals(val)) {
                    int reps = readRunLen();

                    for (int j = 0; j < reps; j++) {
                        consume(i + j, val);
                    }
                    i += reps;
                }
                prev = val;
            }
        }
    }
    //Note: stolen from FLIF's symbol.hpp
    public class NzCoder
    {
        private const int bits = 16; //max value bits

        public BitChance Zero;
        public BitChance Sign;
        public BitChance[] Exp = new BitChance[(bits - 1) * 2];
        public BitChance[] Mant = new BitChance[bits];

        /// <summary> Creates a new nz context and initializes all bit chances to 50%.</summary>
        public NzCoder()
        {
            static BitChance C() => new BitChance(0.5);
            Zero = C();
            Sign = C();

            for (int i = 0; i < bits - 1; i++) {
                Exp[i * 2 + 0] = C();
                Exp[i * 2 + 1] = C();
            }
            for (int i = 0; i < bits; i++) {
                Mant[i] = C();
            }
        }

        private ref BitChance ExpProb(int i, bool sign)
            => ref Exp[(i << 1) + (sign ? 1 : 0)];

        public void Write(ArithmEncoder ac, int value, int min, int max)
        {
            Debug.Assert(min <= max);
            Debug.Assert(value >= min);
            Debug.Assert(value <= max);

            // avoid doing anything if the value is already known
            if (min == max) return;

            if (value == 0) { // value is zero
                Zero.Write(ac, true);
                return;
            }
            if (min <= 0 && max >= 0) {
                // only output zero bit if value could also have been zero
                Zero.Write(ac, false);
            }

            bool sign = value > 0;
            if (min < 0 && max > 0) {
                // only output sign bit if value can be both pos and neg
                Sign.Write(ac, sign);
            }
            if (sign) min = 1;
            if (!sign) max = -1;

            int a = Abs(value);
            int e = Log2(a);
            int amin = sign ? Abs(min) : Abs(max);
            int amax = sign ? Abs(max) : Abs(min);

            int emax = Log2(amax);
            int i = Log2(amin);

            for (; i < emax; i++) {
                // if exponent >i is impossible, we are done
                //if ((1 << (i + 1)) > amax) break;
                Debug.Assert(!((1 << (i + 1)) > amax));

                // if exponent i is possible, output the exponent bit
                ExpProb(i, sign).Write(ac, i == e);
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
                    Mant[pos].Write(ac, bit);
                }
                have |= (bit << pos);
            }
        }
        
        public int Read(ArithmDecoder ac, int min, int max)
        {
            Debug.Assert(min <= max);
            if (min == max) return min;

            bool canBeZero = min <= 0 && max >= 0;
            if (canBeZero && Zero.Read(ac)) return 0;

            bool sign = min >= 0 || (max > 0 && Sign.Read(ac));

            int amin = 1;
            int amax = sign ? Abs(max) : Abs(min);

            int emax = Log2(amax);
            int e = 0;

            for (; e < emax; e++) {
                // if exponent >e is impossible, we are done
                // actually that cannot happen
                //if ((1 << (e+1)) > amax) break;
                if (ExpProb(e, sign).Read(ac)) break;
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
                    if (Mant[pos].Read(ac)) have = minabs1;
                } // else 0-bit is impossible, so bit stays 1
                else have = minabs1;
            }
            return (sign ? have : -have);
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

    public struct BitChance
    {
        public ushort Value;
        public ushort Count;

        public BitChance(int value)
        {
            Debug.Assert(value is >= 0 and < K);
            Value = (ushort)value;
            Count = 0;
        }
        public BitChance(double value)
        {
            Debug.Assert(value is >= 0 and <= 1.0);
            Value = (ushort)(value * K);
            Count = 0;
        }

        public void Write(ArithmEncoder ac, bool bit)
        {
            ac.Write(bit, Value);
            Update(bit);
        }
        public void Write(ArithmEncoder ac, int bit)
        {
            Write(ac, bit != 0);
        }
        public bool Read(ArithmDecoder ac)
        {
            bool bit = ac.ReadBool(Value);
            Update(bit);
            return bit;
        }

        public void Update(bool bit)
        {
            const int DELTA = 3;
            const int LIMIT = 29;

            if (Count < LIMIT) Count++;

            int n = bit ? 0 : K;
            Value += (ushort)((n - Value) / (Count + DELTA));
        }

        public override string ToString() => $"{Value * 100 / K}%";
    }
}
