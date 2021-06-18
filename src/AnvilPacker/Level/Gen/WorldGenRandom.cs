using System;

namespace AnvilPacker.Level.Gen
{
    public class WorldGenRandom
    {
        const long MULTIPLIER = 0x5DEECE66DL;
        const long ADDEND = 0xBL;
        const long MASK = (1L << 48) - 1;

        const double DOUBLE_UNIT = 1.0 / (1L << 53);

        private long _state;

        public WorldGenRandom()
            : this(Environment.TickCount)
        {
        }
        public WorldGenRandom(long seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(long seed)
        {
            _state = (seed ^ MULTIPLIER) & MASK;
        }

        public void SetBaseChunkSeed(int x, int z)
        {
            SetSeed(x * 341873128712L + z * 132897987541L);
        }

        private int Next(int bits)
        {
            _state = (_state * MULTIPLIER + ADDEND) & MASK;
            return (int)(_state >> (48 - bits));
        }

        public int NextInt(int bound)
        {
            int r = Next(31);
            int m = bound - 1;
            if ((bound & m) == 0) {  // i.e., bound is a power of 2
                r = (int)((bound * (long)r) >> 31);
            } else {
                int u = r;
                while (u - (r = u % bound) + m < 0) {
                    u = Next(31);
                }
            }
            return r;
        }
        public int NextInt()
        {
            return Next(31);
        }

        public double NextDouble()
        {
            return (((long)Next(26) << 27) + Next(27)) * DOUBLE_UNIT;
        }
        public long NextLong()
        {
            return ((long)Next(32) << 32) + Next(32);
        }

        public void Skip(int count)
        {
            //https://github.com/Cubitect/cubiomes/blob/6fda1caff80f7e350e0f9bf8e770199d7ea9ccb2/javarnd.h#L72
            //https://www.nayuki.io/page/fast-skipping-in-a-linear-congruential-generator
            long m = 1;
            long a = 0;
            long im = MULTIPLIER;
            long ia = ADDEND;

            for (int k = count; k != 0; k >>= 1) {
                if ((k & 1) != 0) {
                    m *= im;
                    a = im * a + ia;
                }
                ia = (im + 1) * ia;
                im *= im;
            }
            _state = (_state * m + a) & MASK;
        }
    }
}