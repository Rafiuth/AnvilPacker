using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Provides an efficient binary data reader over <see cref="Stream" />. </summary>
    public class DataReader : IDisposable
    {
        private const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
        private const MethodImplOptions NoInline = MethodImplOptions.NoInlining;

        private byte[] _buf;
        private int _bufPos, _bufLen;

        private bool _leaveOpen;

        public Stream BaseStream { get; }

        public long Position
        {
            get => BaseStream.Position - _bufLen + _bufPos;
            set {
                _bufPos = _bufLen = 0;
                BaseStream.Position = value;
            }
        }
        public long length
        {
            get => BaseStream.Length + _bufPos;
        }

        /// <param name="leaveOpen">If true, <paramref name="stream"/> will be disposed when <see cref="Dispose"/> is called. </param>
        /// <param name="bufferSize">Size of the internal buffer. Can be disabled by setting it to 0, but note that disabling buffering will increase overhead when working small primitives. </param>
        public DataReader(Stream stream, bool leaveOpen = false, int bufferSize = 4096)
        {
            BaseStream = stream;
            _buf = new byte[bufferSize];
            _bufPos = _bufLen = 0;
            _leaveOpen = leaveOpen;
        }

        public void ReadBytes(Span<byte> dest)
        {
            int bufAvail = Math.Min(dest.Length, _bufLen - _bufPos);
            if (bufAvail > 0) {
                _buf.AsSpan(_bufPos, bufAvail).CopyTo(dest);
                _bufPos += bufAvail;
                dest = dest[bufAvail..];
            }
            while (dest.Length > 0) {
                int bytesRead = BaseStream.Read(dest);
                if (bytesRead <= 0) {
                    throw new EndOfStreamException();
                }
                dest = dest[bytesRead..];
            }
        }
        private void FillBuffer()
        {
            if (_bufPos >= _bufLen) {
                _bufLen = BaseStream.Read(_buf);
                _bufPos = 0;
            }
        }

        [MethodImpl(Inline)]
        private unsafe T Read<T>() where T : unmanaged
        {
            if (_bufPos + sizeof(T) < _bufLen) {
                T value = Mem.Read<T>(_buf, _bufPos);
                _bufPos += sizeof(T);
                return value;
            }
            return ReadUnbuffered<T>();
        }
        [MethodImpl(NoInline)]
        private T ReadUnbuffered<T>() where T : unmanaged
        {
            FillBuffer(); //fill buffer before to so we may avoid 2 stream reads
            Unsafe.SkipInit(out T value);
            ReadBytes(Mem.CreateSpan<T, byte>(ref value, 1));
            return value;
        }

        [MethodImpl(Inline)]
        private T ReadLE<T>() where T : unmanaged
        {
            T value = Read<T>();
            if (!BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            return value;
        }
        [MethodImpl(Inline)]
        private T ReadBE<T>() where T : unmanaged
        {
            T value = Read<T>();
            if (BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            return value;
        }

        public void Dispose()
        {
            if (!_leaveOpen) {
                BaseStream.Dispose();
            }
        }

        //Convenience functions
        //TODO: better naming
        //ReadU16LE / ReadUShortLE  < both are equally awful
        //ReadS16LE / ReadShortLE   < 2nd is easier to type and read
        // ^^^^ consistent and pretty, endianness postfix makes it ugly
        //ReadLU16  ReadBU16  < ugly

        public byte ReadByte()       => Read<byte>();
        public sbyte ReadSByte()     => Read<sbyte>();

        public short ReadShortLE()   => ReadLE<short>();
        public int ReadIntLE()       => ReadLE<int>();
        public long ReadLongLE()     => ReadLE<long>();
        public ushort ReadUShortLE() => ReadLE<ushort>();
        public uint ReadUIntLE()     => ReadLE<uint>();
        public ulong ReadULongLE()   => ReadLE<ulong>();
        public float ReadFloatLE()   => ReadLE<float>();
        public double ReadDoubleLE() => ReadLE<double>();

        
        public short ReadShortBE()   => ReadBE<short>();
        public int ReadIntBE()       => ReadBE<int>();
        public long ReadLongBE()     => ReadBE<long>();

        public ushort ReadUShortBE() => ReadBE<ushort>();
        public uint ReadUIntBE()     => ReadBE<uint>();
        public ulong ReadULongBE()   => ReadBE<ulong>();
        public float ReadFloatBE()   => ReadBE<float>();
        public double ReadDoubleBE() => ReadBE<double>();

        public byte[] ReadBytes(int count)
        {
            var buf = new byte[count];
            ReadBytes(buf);
            return buf;
        }
        public void ReadBytes(byte[] buffer, int offset, int count)
        {
            ReadBytes(buffer.AsSpan(offset, count));
        }

        //Java compat

        /// <summary> Reads an UTF8 string prefixed with a big-endian ushort indicating its length </summary>
        public string ReadString() => ReadString(ReadUShortBE());

        /// <summary> Reads an UTF8 string of <paramref name="len"/> bytes. </summary>
        public string ReadString(int len)
        {
            Span<byte> buf = len <= 256 ? stackalloc byte[len] : new byte[len];
            ReadBytes(buf);
            return Encoding.UTF8.GetString(buf);
        }
    }
}