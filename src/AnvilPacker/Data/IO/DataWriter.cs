using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Provides an efficient binary data writer over <see cref="Stream" />. </summary>
    public class DataWriter : IDisposable
    {
        private const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
        private const MethodImplOptions NoInline = MethodImplOptions.NoInlining;

        private byte[] _buf;
        private int _bufPos;

        private bool _leaveOpen;
        
        private Stream _stream;
        
        /// <summary> Returns the stream in which the data is being written to. </summary>
        /// <remarks>
        /// Accessing this property causes the buffer to be flushed. Data can be written directly 
        /// to this stream, for as long as this writer is not used.
        /// </remarks>
        public Stream BaseStream
        {
            get {
                Flush();
                return _stream;
            }
        }

        public long Position
        {
            get => _stream.Position + _bufPos;
            set {
                Flush();
                _stream.Position = value;
            }
        }
        public long Length
        {
            get => _stream.Length + _bufPos;
            set {
                Flush();
                _stream.SetLength(value);
            }
        }
        /// <param name="leaveOpen">If true, <paramref name="stream"/> will be disposed when <see cref="Dispose"/> is called. </param>
        /// <param name="bufferSize">Size of the internal buffer. Can be disabled by setting it to 0, but note that disabling buffering will increase overhead when working small primitives. </param>
        public DataWriter(Stream stream, bool leaveOpen = false, int bufferSize = 4096)
        {
            _stream = stream;
            _buf = new byte[bufferSize];
            _bufPos = 0;
            _leaveOpen = leaveOpen;
        }

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length < _buf.Length - _bufPos) {
                data.CopyTo(_buf.AsSpan(_bufPos));
                _bufPos += data.Length;
            } else {
                Flush();
                _stream.Write(data);
            }
        }

        public void Flush()
        {
            if (_bufPos > 0) {
                _stream.Write(_buf, 0, _bufPos);
            }
            _bufPos = 0;
        }

        [MethodImpl(Inline)]
        private unsafe void Write<T>(T value) where T : unmanaged
        {
            if (_bufPos + sizeof(T) < _buf.Length) {
                Mem.Write<T>(_buf, _bufPos, value);
                _bufPos += sizeof(T);
                return;
            }
            WriteUnbuffered<T>(value);
        }
        [MethodImpl(NoInline)]
        private void WriteUnbuffered<T>(T value) where T : unmanaged
        {
            WriteBytes(Mem.CreateSpan<T, byte>(ref value, 1));
        }

        [MethodImpl(Inline)]
        public void WriteLE<T>(T value) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            Write(value);
        }
        [MethodImpl(Inline)]
        public void WriteBE<T>(T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            Write(value);
        }

        public void Dispose()
        {
            Flush();
            if (!_leaveOpen) {
                _stream.Dispose();
            }
        }

        //Convenience functions
        public void WriteByte(int x)        => Write((byte)x);
        public void WriteSByte(int x)       => Write((sbyte)x);
        public void WriteBool(bool x)       => WriteByte(x ? 1 : 0);

        public void WriteShortLE(int x)     => WriteLE((short)x);
        public void WriteIntLE(int x)       => WriteLE((int)x);
        public void WriteLongLE(long x)     => WriteLE((long)x);
        
        public void WriteUShortLE(int x)    => WriteLE((ushort)x);
        public void WriteUIntLE(uint x)     => WriteLE((uint)x);
        public void WriteULongLE(ulong x)   => WriteLE((ulong)x);
        public void WriteFloatLE(float x)   => WriteLE((float)x);
        public void WriteDoubleLE(double x) => WriteLE((double)x);
        
        public void WriteShortBE(int x)     => WriteBE((short)x);
        public void WriteIntBE(int x)       => WriteBE((int)x);
        public void WriteLongBE(long x)     => WriteBE((long)x);
        
        public void WriteUShortBE(int x)    => WriteBE((ushort)x);
        public void WriteUIntBE(uint x)     => WriteBE((uint)x);
        public void WriteULongBE(ulong x)   => WriteBE((ulong)x);
        public void WriteFloatBE(float x)   => WriteBE((float)x);
        public void WriteDoubleBE(double x) => WriteBE((double)x);

        public void WriteBytes(byte[] buffer, int offset, int count)
        {
            WriteBytes(buffer.AsSpan(offset, count));
        }

        public unsafe void WriteBulkLE<T>(ReadOnlySpan<T> buf) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian || sizeof(T) <= 1) {
                WriteBytes(MemoryMarshal.AsBytes(buf));
                return;
            }
            //TODO: optimize by copying into _buf then bswap'ing
            for (int i = 0; i < buf.Length; i++) {
                WriteLE(buf[i]);
            }
        }
        public unsafe void WriteBulkBE<T>(ReadOnlySpan<T> buf) where T : unmanaged
        {
            if (!BitConverter.IsLittleEndian || sizeof(T) <= 1) {
                WriteBytes(MemoryMarshal.AsBytes(buf));
                return;
            }
            for (int i = 0; i < buf.Length; i++) {
                WriteBE(buf[i]);
            }
        }
        //Java compat

        /// <summary> Writes an UTF8 string prefixed with a big-endian ushort indicating it's length. </summary>
        public void WriteUTF(string str)
        {
            WriteString(str, (dw, len) => {
                Ensure.That(len < 65536, "Encoded string cannot be longer than 65535 bytes.");
                dw.WriteUShortBE(len);
            });
        }

        /// <summary> Writes an UTF8 string with a custom length prefix. </summary>
        public void WriteString(string str, Action<DataWriter, int> writePrefixLen)
        {
            var enc = Encoding.UTF8;
            int len = enc.GetByteCount(str);

            var buf = len < 256 ? stackalloc byte[len] : new byte[len];
            enc.GetBytes(str, buf);

            writePrefixLen(this, len);
            WriteBytes(buf);
        }
        /// <summary> Writes an UTF8 encoded string, postfixed '\0'. The string should not contain any NUL char. </summary>
        public void WriteNulString(string str)
        {
            WriteString(str, (dw, len) => { });
            WriteByte(0);
        }
    }
}
