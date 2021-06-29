using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    using static AnvilPacker.Data.Entropy.ArithmCoderConsts;

    //stolen from FLIF's symbol.hpp
    public class NzCoder
    {
        private const int bits = 16; //max value bits

        public BitChance Zero;
        public readonly BitChance[] Exp = new BitChance[bits];
        public readonly BitChance[] Mant = new BitChance[bits];

        private static readonly BitChance 
            InitialZeroChance = new(0.5, 48, 21),
            InitialExpChance  = new(0.5, 32, 4),
            InitialMantChance = new(0.5, 16, 4);

        /// <summary> Creates a new nz context and initializes all bit chances to 50%.</summary>
        public NzCoder()
        {
            Zero = InitialZeroChance;
            Exp.Fill(InitialExpChance);
            Mant.Fill(InitialMantChance);
        }

        public void Write(ArithmEncoder ac, int value, int max)
        {
            Debug.Assert(value >= 0 && value <= max);
            Debug.Assert(max >= 0 && max < (1 << bits));

            // avoid doing anything if the value is already known
            if (max == 0) return;

            Zero.Write(ac, value == 0);
            if (value == 0) return;

            int e = Log2(value);
            int emax = Log2(max);
            int i = 0;

            for (; i < emax; i++) {
                // if exponent >i is impossible, we are done
                //if ((1 << (i + 1)) > amax) break;
                Debug.Assert(!((1 << (i + 1)) > max));

                Exp[i].Write(ac, i == e);
                if (i == e) break;
            }

            int have = 1 << e;
            while (e-- > 0) {
                int left = 1 << e;
                int minabs1 = have | left;
                int maxabs0 = have | (left - 1);

                if (minabs1 <= max && maxabs0 > 0) { // 0-bit and 1-bit are both possible
                    int bit = (value >> e) & 1;
                    Mant[e].Write(ac, bit);
                    have |= (bit << e);
                }
            }
        }

        public int Read(ArithmDecoder ac, int max)
        {
            Debug.Assert(max >= 0 && max < (1 << bits));
            if (max == 0 || Zero.Read(ac)) return 0;

            int emax = Log2(max);
            int e = 0;
            
            for (; e < emax; e++) {
                if (Exp[e].Read(ac)) break;
            }
            
            int have = 1 << e;
            while (e-- > 0) {
                int left = 1 << e;
                int minabs1 = have | left;
                int maxabs0 = have | (left - 1);

                if (minabs1 <= max && maxabs0 > 0 && Mant[e].Read(ac)) {
                    have = minabs1;
                }
            }
            return have;
        }

        private static int Log2(int x) => BitOperations.Log2((uint)x);

        public override string ToString()
        {
            var exp = string.Join(" ", Exp.AsEnumerable());
            var mant = string.Join(" ", Mant.AsEnumerable());
            return $"Z: {Zero},\nExp: [{exp}]\nMant: [{mant}]"
                        .Replace("%", ""); //improve readability
        }
    }

    public struct BitChance
    {
        public ushort Value;
        public ushort Count;

        public ushort Limit, Delta;

        public BitChance(int value, int limit = 29, int delta = 3)
        {
            Debug.Assert(value is >= 0 and < K);
            Value = (ushort)value;
            Count = 0;

            Limit = (ushort)limit;
            Delta = (ushort)delta;
        }
        public BitChance(double value, int limit = 29, int delta = 3)
            : this((int)(value * K), limit, delta)
        {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(bool bit)
        {
            if (Count < Limit) Count++;

            int n = bit ? 0 : K;
            Value += (ushort)((n - Value) / (Count + Delta));
        }

        public override string ToString() => $"{Value * 100 / K}%";
    }
}