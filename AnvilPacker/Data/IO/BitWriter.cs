using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public class BitWriter : IDisposable
    {
        private const int BUFFER_SIZE = 4096 - 8;
        private byte[] _buf = new byte[BUFFER_SIZE + 8]; //we will write +8 bytes beyond _len
        private int _pos; //current bit pos

        private readonly DataWriter _stream;
        private readonly bool _leaveOpen;

        public BitWriter(DataWriter stream, bool leaveOpen = true)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        /// <summary> Writes <paramref name="count"/> bits to the stream. </summary>
        /// <param name="count">Number of bits to write. Must be between 0 and 32 (inclusive) </param>
        public void WriteBits(int value, int count)
        {
            Debug.Assert(count is >= 0 and <= 32);

            ulong v = Mem.ReadLE<ulong>(_buf, _pos >> 3);

            ulong mask = (1ul << count) - 1;
            int shift = _pos & 7;
            v |= ((ulong)value & mask) << shift;

            Mem.WriteLE<ulong>(_buf, _pos >> 3, v);

            _pos += count;

            if (_pos >= BUFFER_SIZE << 3) {
                FlushBuffer(false);
            }
        }

        /// <summary> Writes a single bit to the stream. </summary>
        public void WriteBit(int value)
        {
            WriteBits(value, 1);
        }

        public void WriteBit(bool value)
        {
            WriteBit(Mem.As<bool, byte>(value));
        }

        /// <summary> Aligns the buffer position to the next byte boundary. </summary>
        public void Align()
        {
            _pos = (_pos + 7) & ~7;
        }

        public void WriteAligned(ReadOnlySpan<byte> buffer)
        {
            FlushBuffer(true);
            _stream.WriteBytes(buffer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        ///<param name="force">Whether to force alignment on byte boundaries. If false, remaining bits will be moved back to the buffer start. </param>
        private void FlushBuffer(bool force)
        {
            int count = _pos >> 3;
            int remBits = _pos & 7;
            if (force) {
                count += (remBits + 7) >> 3;
            }
            _stream.WriteBytes(_buf.AsSpan(0, count));

            if (!force && remBits > 0) {
                //   flushed     remaining (=3)
                // [xxxx_xxxx] [xxx0_0000]
                //                 ^ _pos
                ulong v = Mem.ReadLE<ulong>(_buf, count);
                ulong mask = (1ul << remBits) - 1;
                Mem.WriteLE<ulong>(_buf, 0, v & mask);

                _pos = remBits;
                _buf.AsSpan(8, count - 7).Clear(); //skip the ulong we just wrote, and include the remaining byte
            } else {
                _pos = 0;
                _buf.AsSpan(0, count).Clear();
            }
        }

        public void Flush()
        {
            FlushBuffer(true);
        }

        public void Dispose()
        {
            Flush();

            if (!_leaveOpen) {
                _stream.Dispose();
            }
        }
    }
}
