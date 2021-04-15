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

        internal const int BUF_SIZE = 1024;
    }
    //https://encode.su/threads/1821-Asymetric-Numeral-System/page8
    //https://pastebin.com/FnNK4HDc
    /// <summary> Implementation of the <c>Arithmetic entropy encoder</c> for binary data. </summary>
    public class ArithmEncoder
    {
        private uint low, hi;
        private int bufPos;
        private DataWriter stream;
        private byte[] buf = new byte[BUF_SIZE];

        public ArithmEncoder(DataWriter outStream)
        {
            stream = outStream;

            low = 0u;
            hi = ~0u;
            bufPos = 0;
        }

        /// <summary> Writes a bit to the output. </summary>
        /// <param name="s">Bit to encode, any non zero value will be interpreted as being <c>1</c>. </param>
        /// <param name="p0">Probability of <c>s</c> being 0. Must range between <c>[0..<see cref="K"/> - 1]</c> </param>
        public void Write(int s, int p0)
        {
            bool bs = Mem.As<int, bool>(s);
            Write(bs, p0);
        }
        public void Write(bool s, int p0)
        {
            Debug.Assert(p0 >= 0 && p0 < K, $"Probability must be >= 0 and < {K}.");
            Debug.Assert(s || p0 != 0, "Probability cannot be 0 when s == 0");

            Write(s, (uint)p0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(bool s, uint p0)
        {
            uint split = low + (uint)((ulong)(hi - low) * p0 >> 16);
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

            stream.WriteBytes(Mem.CreateSpan(ref buf[0], bufPos));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteU16(uint x)
        {
            Mem.WriteLE(buf, bufPos, (ushort)x);
            bufPos += 2;
            if (bufPos >= BUF_SIZE) {
                stream.WriteBytes(buf);
                bufPos = 0;
            }
        }
    }

    public class ArithmDecoder
    {
        private uint low, hi, x;
        private int bufPos, bufLen;
        private Stream stream;
        private byte[] buf = new byte[BUF_SIZE];

        /// <param name="inStream">This stream should implement efficient ReadByte()s </param>
        public void Init(Stream inStream)
        {
            stream = inStream;
            bufLen = 0;

            low = 0u;
            hi = ~0u;
            x = ReadU16() << 16 |
                ReadU16() << 0;
        }

        /// <summary>Reads a bit from the stream. </summary>
        /// <remarks>Due to the nature of ANS, the data will be decoded in the reverse order it was originally encoded.</remarks>
        /// <param name="p0">Probability of the result being 0. Must range between <c>[0..<see cref="K"/> - 1]</c> </param>
        public int Read(int p0)
        {
            bool result = ReadBool(p0);
            return Mem.As<bool, int>(result);
        }
        public bool ReadBool(int p0)
        {
            Debug.Assert(p0 >= 0 && p0 < K, $"Probability must be >= 0 and < {K}.");
            return Read((uint)p0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Read(uint p0)
        {
            uint split = low + (uint)((ulong)(hi - low) * p0 >> 16);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadU16()
        {
            if (bufPos + 2 <= bufLen) {
                ushort result = Mem.ReadLE<ushort>(buf, bufPos);
                bufPos += 2;
                return result;
            }
            return FillBufAndReadU16();
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private uint FillBufAndReadU16()
        {
            var span = buf.AsSpan();
            int rem = bufLen - bufPos;
            if (rem > 0) {
                //shift remaining data to beginning
                //span.Slice(bufPos, rem).CopyTo(span);
                Debug.Assert(rem == 1);
                span[0] = span[bufPos];
                span = span[rem..];
            }
            bufPos = 0;
            bufLen = stream.Read(span) + rem;
            if (bufLen <= 0) {
                throw new EndOfStreamException();
            }
            return ReadU16();
        }
    }
}
