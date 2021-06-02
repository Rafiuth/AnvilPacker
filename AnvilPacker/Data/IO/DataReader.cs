using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Provides an efficient binary data reader over <see cref="Stream" />. </summary>
    //Design note: this class doesn't allow abstraction because it's whole point
    //is to provide a "efficient" way to read binary data.
    public partial class DataReader : IDisposable
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
        public long Length
        {
            get => BaseStream.Length;
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
            FillBuffer(); //fill buffer before so we might call Read() once
            Unsafe.SkipInit(out T value);
            ReadBytes(Mem.CreateSpan<T, byte>(ref value, 1));
            return value;
        }

        [MethodImpl(Inline)]
        public T ReadLE<T>() where T : unmanaged
        {
            T value = Read<T>();
            if (!BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            return value;
        }
        [MethodImpl(Inline)]
        public T ReadBE<T>() where T : unmanaged
        {
            T value = Read<T>();
            if (BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            return value;
        }

        public virtual void Dispose()
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
        public bool ReadBool()       => ReadByte() != 0;

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

        public void ReadBulkLE<T>(Span<T> buf) where T : unmanaged
        {
            ReadBulk(buf, !BitConverter.IsLittleEndian);
        }
        public void ReadBulkBE<T>(Span<T> buf) where T : unmanaged
        {
            ReadBulk(buf, BitConverter.IsLittleEndian);
        }

        private unsafe void ReadBulk<T>(Span<T> buf, bool revElemBytes) where T : unmanaged
        {
            ReadBytes(MemoryMarshal.AsBytes(buf));
            if (revElemBytes && sizeof(T) > 1) {
                Mem.BSwapBulk(buf);
            }
        }

        //Java compat

        /// <summary> Reads an UTF8 string prefixed with a big-endian ushort indicating its length </summary>
        public string ReadUTF() => ReadString(ReadUShortBE());

        /// <summary> Reads an UTF8 string of <paramref name="len"/> bytes. </summary>
        public string ReadString(int len)
        {
            Span<byte> buf = len <= 256 ? stackalloc byte[len] : new byte[len];
            ReadBytes(buf);
            return Encoding.UTF8.GetString(buf);
        }
        public string ReadNulString()
        {
            var buf = new byte[256];
            int pos = 0;

            while (true) {
                byte b = ReadByte();
                if (b == 0) break;

                if (pos >= buf.Length) {
                    Array.Resize(ref buf, buf.Length * 2);
                }
                buf[pos++] = b;
            }
            return Encoding.UTF8.GetString(buf, 0, pos);
        }
    }
}