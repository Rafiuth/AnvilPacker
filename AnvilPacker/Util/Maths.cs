using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    /// <summary> Math extensions </summary>
    public static class Maths
    {
        public static int CeilLog2(int x)
        {
            return x == 0 ? 0 : BitOperations.Log2((uint)x - 1) + 1;
        }

        public static bool IsPow2(int x)
        {
            return (x & (x - 1)) == 0;
        }

        /// <summary> Rounds x to the smallest encompassing power of two. </summary>
        public static int RoundUpPow2(int x)
        {
            return 1 << CeilLog2(x);
        }

        /// <summary> Returns `ceil(x / y)` of two positive integers. </summary>
        public static int CeilDiv(int x, int y)
        {
            return (x + (y - 1)) / y;
        }

        public static int Min(int x, int y)
        {
            return x < y ? x : y;
        }
        public static int Max(int x, int y)
        {
            return x > y ? x : y;
        }

        public static int Abs(int x)
        {
            //return x < 0 ? -x : x;
            //RyuJIT doesn't emit cmovs yet, one must use arcaic tricks instead.
            //https://stackoverflow.com/a/9772647
            int mask = x >> 31;
            return (x + mask) ^ mask;
        }
        public static int Sign(int x)
        {
            return (x >> 31) | (int)((uint)(-x) >> 31);
        }

        public static int GetNibble(byte[] arr, int idx)
        {
            //byte v = arr[idx / 2];
            //return i % 2 == 0 ? v & 15 : v >> 4;

            int s = (idx & 1) << 2; //(idx % 2) * 4
            return (arr[idx >> 1] >> s) & 0xF;
        }
        public static void SetNibble(byte[] arr, int idx, int val)
        {
            ref byte b = ref arr[idx >> 1];

            int s = (idx & 1) << 2; //(idx % 2) * 4
            b = (byte)((b & (0xF0 >> s)) | (val << s));
        }
    }
}
