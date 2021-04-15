using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Entropy
{
    using static UAbsConsts;
    //https://encode.su/threads/1821-Asymetric-Numeral-System/page8
    //https://pastebin.com/FnNK4HDc
    public static class UAbsConsts
    {
        public const int L_PROB = 16; //probability bit-precision for uABS 
        public const int L_IO = 16;

        public const uint BaseX = 1u << L_PROB;
        public const uint K = 1u << L_PROB;
    }

    //TODO: use stream+buf like ArithmCoder
    /// <summary> <c>Uniform Asymmetric Binary System</c> encoder </summary>
    public ref struct UAbsEncoder
    {
        private uint _x;
        private byte[] _buf;
        private int _pos;

        /// <summary> Writes a bit to the output. </summary>
        /// <param name="s">Bit to encode, any non zero value will be interpreted as being <c>1</c>. </param>
        /// <param name="p0">Probability of <c>s</c> being 0. Must range between <c>[1..<see cref="K"/> - 1]</c> </param>
        public void Write(int s, int p0)
        {
            bool bs = Mem.As<int, bool>(s);
            Write(bs, p0);
        }
        public void Write(bool s, int p0)
        {
            Debug.Assert(p0 > 0 && p0 < K, $"Probability must be > 0 and < {K}.");
            Write(s, (uint)p0);
        }
        private void Write(bool s, uint p0)
        {
            uint q0 = K - p0;
            uint x = _x;

            uint threshold = (s ? q0 : p0) << L_IO;
            while (x >= threshold) {
                Flush(x);
                x >>= L_IO;
            }
            ulong xk = (ulong)x * K;
            _x = (uint)(s ? xk / q0 : (xk + K - 1) / p0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Flush(uint x)
        {
            if (_pos >= 2) { //hot path
                _pos -= 2;
                Mem.WriteLE(_buf, _pos, (ushort)x);
                return;
            }
            ExpandAndFlush(x);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ExpandAndFlush(uint x)
        {
            var newBuf = new byte[_buf.Length * 2];
            Array.Copy(_buf, 0, newBuf, _buf.Length, _buf.Length);
            _pos = newBuf.Length - _buf.Length;
            _buf = newBuf;

            Flush(x);
        }

        /// <summary> Inits or resets the encoder. Must be called before data is encoded. </summary>
        /// <param name="discardBuf">Whether to create a new buffer instead of reusing the previous one. May be desirable if the buffer grows too large.</param>
        public void Init(bool discardBuf = false)
        {
            if (_buf == null || discardBuf) {
                _buf = new byte[4096];
            }
            _pos = _buf.Length;
            _x = BaseX;
        }
        /// <summary> Flushes the encoder state and returns its buffer. </summary>
        /// <remarks>The state is a 32-bit value, this method will always add a 4 byte overhead.</remarks>
        public ArraySegment<byte> Finish()
        {
            Flush(_x);
            Flush(_x >> 16);
            return new ArraySegment<byte>(_buf, _pos, _buf.Length - _pos);
        }
    }

    /// <summary> <c>Uniform Asymmetric Binary System</c> decoder </summary>
    public ref struct UAbsDecoder
    {
        private uint _x;
        private byte[] _buf;
        private int _pos;

        public void Init(byte[] buf, int offset = 0)
        {
            _buf = buf;
            _pos = offset + 4;

            _x = 0;
            for (int i = 0; i < 2; i++) {
                _x = (_x << 16) | Mem.ReadLE<ushort>(_buf, offset);
                offset += 2;
            }
        }

        /// <summary>Reads a bit from the stream. </summary>
        /// <remarks>Due to the nature of ANS, the data will be decoded in the reverse order it was originally encoded.</remarks>
        /// <param name="p0">Probability of the result being 0. Must range between <c>[1..<see cref="K"/> - 1]</c> </param>
        public int Read(int p0)
        {
            return (int)Read((uint)p0);
        }
        private uint Read(uint p0)
        {
            uint x = _x;
            while (x < BaseX) {
                Debug.Assert(_pos + 2 <= _buf.Length, "Reading past buffer");
                x = (x << L_IO) | Mem.ReadLE<ushort>(_buf, _pos);
                _pos += 2;
            }
            uint q0 = K - p0;
            ulong xp = (ulong)x * q0 - 1;
            uint s = (uint)(((xp & (K - 1)) + q0) >> L_PROB);  // <= frac(xp) < p0
            xp = (xp >> L_PROB) + 1;
            _x = (uint)(s != 0 ? xp : x - xp);
            return s;
        }
    }
}
