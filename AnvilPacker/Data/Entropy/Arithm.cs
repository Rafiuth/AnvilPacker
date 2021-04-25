using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Entropy
{
    using static ArithmCoderConsts;
    public static class ArithmCoderConsts
    {
        /// <summary> Max probability value. </summary>
        public const int K = 1 << 16;
    }
    public class ArithmEncoder
    {
        private uint low, hi;
        private DataWriter stream;

        public ArithmEncoder(DataWriter outStream)
        {
            stream = outStream;

            low = 0u;
            hi = ~0u;
        }

        /// <summary> Encodes a bit with the given probability. </summary>
        /// <param name="s">Bit to encode, any non zero value will be interpreted as being <c>1</c>. </param>
        /// <param name="p">Probability of <c>s</c> being 0. Must range between <c>[0..<see cref="K"/> - 1]</c> </param>
        public void Write(int s, int p)
        {
            Write(s != 0, p);
        }
        public void Write(bool s, int p)
        {
            Debug.Assert(p >= 0 && p < K, $"Probability must be >= 0 and < {K}.");
            Debug.Assert(s || p != 0, "Probability cannot be 0 when s == 0");

            Write(s, (uint)p);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(bool s, uint p)
        {
            uint split = low + (uint)((ulong)(hi - low) * p >> 16);
            if (s) {
                low = split + 1;
            } else {
                hi = split;
            }
            if ((low ^ hi) < 0x10000) {
                WriteU16(hi >> 16);
                low <<= 16;
                hi = (hi << 16) | 0xFFFF;
            }
        }

        public void Flush()
        {
            WriteU16(hi >> 16);
            WriteU16(hi >> 0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteU16(uint x)
        {
            stream.WriteUShortLE((int)x);
        }
    }

    public class ArithmDecoder
    {
        private uint low, hi, x;
        private DataReader stream;
        
        /// <param name="length">If greater than zero, defines the maximum number of bytes that can read from the stream. </param>
        public ArithmDecoder(DataReader inStream, int length = 0)
        {
            stream = inStream;

            low = 0u;
            hi = ~0u;
            x = ReadU16() << 16 |
                ReadU16() << 0;
        }

        /// <summary> Decodes a bit with the given probability. </summary>
        /// <param name="p">Probability of the result being 0. Must range between <c>[0..<see cref="K"/> - 1]</c> </param>
        public int Read(int p)
        {
            bool result = ReadBool(p);
            return Mem.As<bool, int>(result);
        }
        public bool ReadBool(int p)
        {
            Debug.Assert(p >= 0 && p < K, $"Probability must be >= 0 and < {K}.");
            return Read((uint)p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Read(uint p)
        {
            uint split = low + (uint)((ulong)(hi - low) * p >> 16);
            bool result = x > split;
            if (result) {
                low = split + 1;
            } else {
                hi = split;
            }
            if ((low ^ hi) < 0x10000) {
                x = (x << 16) | ReadU16();
                low <<= 16;
                hi = (hi << 16) | 0xFFFF;
            }
            return result;
        }
        private uint ReadU16()
        {
            return stream.ReadUShortLE();
        }
    }
}
