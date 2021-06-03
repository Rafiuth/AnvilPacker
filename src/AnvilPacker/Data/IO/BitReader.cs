using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    //https://fgiesen.wordpress.com/2018/02/19/reading-bits-in-far-too-many-ways-part-1/
    public class BitReader : IDisposable
    {
        private const int BUFFER_SIZE = 4096 - 8;
        private byte[] _buf = new byte[BUFFER_SIZE + 8]; //ReadBits() might read +8 bytes beyond BUFFER_SIZE
        private int _pos; //current bit pos
        private int _len; //bits buffered

        private readonly Stream _stream;
        private readonly bool _leaveOpen;

        public BitReader(Stream stream, bool leaveOpen = true)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        /// <summary> Reads <paramref name="count"/> bits from the stream. </summary>
        /// <param name="count">Number of bits to read. Must be between 0 and 32 (inclusive) </param>
        public int ReadBits(int count)
        {
            Debug.Assert(count is >= 1 and <= 32);

            if (_pos + count > _len) { //TODO: is this branch problematic? (instruction cache or whatever)
                Fetch(count);
            }
            ulong val = PeekChecked(count);
            _pos += count;
            return (int)val;
        }

        /// <summary> Reads a single bit from the stream. </summary>
        public int ReadBit()
        {
            //TODO: worth inlining?
            return ReadBits(1);
        }
        /// <summary> Reads a single bit from the stream. </summary>
        public bool ReadBool()
        {
            return Mem.As<int, bool>(ReadBit());
        }

        public int PeekBits(int count)
        {
            Debug.Assert(count is >= 0 and <= 32);

            if (_pos + count > _len) { //TODO: is this branch problematic? (instruction cache or whatever)
                Fetch(count);
            }
            return (int)PeekChecked(count);
        }
        /// <summary> Advances <paramref name="count"/> bits, assumming <see cref="PeekBits(int)"/> was called before. </summary>
        /// <remarks><see cref="PeekBits(int)"/> must be called before this method to ensure data was buffered. </remarks>
        public void Advance(int count)
        {
            Debug.Assert(count is >= 0 and <= 32);

            _pos += count;
        }
        /// <summary> Skips <paramref name="count"/> bits. Slower but safer than <see cref="Advance(int)"/></summary>
        public void Skip(int count)
        {
            int rem = _len - _pos;
            if (rem >= count) {
                _pos += count;
                count -= rem;
            }
            while (count > 0) {
                Fetch(Math.Min(count, BUFFER_SIZE * 8));

                int availBits = Math.Min(count, _len - _pos);
                count -= availBits;
                _pos += availBits;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong PeekChecked(int count)
        {
            ulong v = Mem.ReadLE<ulong>(_buf, _pos >> 3);
            if (Bmi1.X64.IsSupported) {
                return Bmi1.X64.BitFieldExtract(v, (byte)(_pos & 7), (byte)count);
            }
            ulong mask = (1ul << count) - 1;
            int shift = _pos & 7;
            return (v >> shift) & mask;
        }

        /// <summary> Aligns the buffer position to the next byte boundary. </summary>
        public void Align()
        {
            _pos = (_pos + 7) & ~7;
        }
        /// <summary> Reads raw bytes from the stream. </summary>
        /// <remarks>The stream is aligned to the next byte boundary before data is read to <paramref name="buffer"/>.</remarks>
        public void ReadAligned(Span<byte> buffer)
        {
            Align();
            //TODO: optimize
            //_buf.AsSpan(_pos >> 3, (_len - _pos) >> 3).CopyTo(buffer);
            ReadUnaligned(buffer);
        }

        private void ReadUnaligned(Span<byte> buffer)
        {
            //TODO: optimize
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = (byte)ReadBits(8);
            }
        }

        private void Fetch(int minBits)
        {
            Debug.Assert(minBits <= BUFFER_SIZE * 8);

            int rem = _len - _pos;
            int remBytes = (rem + 7) >> 3;

            if (rem > 0) {
                //shift remaining data to buffer start
                Debug.Assert(rem <= 56);

                int shift = (remBytes << 3) - rem;
                Mem.WriteLE(_buf, 0, PeekChecked(rem) << shift);

                _pos = shift;
            } else {
                _pos = 0;
            }
            int bufPos = remBytes;
            int minBytes = (minBits - rem + 7) >> 3;

            while (bufPos - remBytes < minBytes) {
                int bytesRead = _stream.Read(_buf, bufPos, BUFFER_SIZE - bufPos);

                if (bytesRead <= 0) {
                    throw new EndOfStreamException();
                }
                bufPos += bytesRead;
            }
            _len = bufPos * 8;
        }

        public void Dispose()
        {
            if (!_leaveOpen) {
                _stream.Dispose();
            }
        }
    }
}
