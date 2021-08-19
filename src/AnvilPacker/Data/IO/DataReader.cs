using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Provides an efficient binary data reader over <see cref="Stream" />. </summary>
    public partial class DataReader : IDisposable
    {
        private const MethodImplOptions Inline = MethodImplOptions.AggressiveInlining;
        private const MethodImplOptions NoInline = MethodImplOptions.NoInlining;

        private byte[] _buf;
        private int _bufPos, _bufLen;

        private bool _leaveOpen;
        
        /// <summary> Returns the stream in which the data is being read from. </summary>
        /// <remarks> The position of this stream may be wrong, as this reader might buffer data. Use AsStream() if you need a raw Stream. </remarks>
        public Stream BaseStream { get; }

        public long Position
        {
            get => BaseStream.Position - _bufLen + _bufPos;
            set {
                long currPos = BaseStream.Position;
                long bufStart = currPos - _bufLen;
                long bufEnd = currPos;
                //keep the buffer if possible
                if (value >= bufStart && value < bufEnd) {
                    _bufPos = (int)(value - bufStart);
                    return;
                }
                _bufPos = _bufLen = 0;
                BaseStream.Position = value;
            }
        }
        public long Length
        {
            get => BaseStream.Length;
        }

        /// <param name="leaveOpen">If true, <paramref name="stream"/> will be disposed when <see cref="Dispose"/> is called. </param>
        /// <param name="bufferSize">
        /// Size of the internal buffer. Can be disabled by setting it to 0. <br/>
        /// This value should be tuned depending on the usecase: <br/>
        /// - For frequent random access, a lower value is better because the buffer won't be filled and discarded during seeks. The recomended size would be the amount of data read before each seek. <br/>
        /// - For sequential reads, a higher value is better because less Read() calls will be made to the underlying stream. 1-4KB is recommended. <br/>
        /// That the buffer is only filled after primitive reads - ReadBytes() won't.
        /// </param>
        public DataReader(Stream stream, bool leaveOpen = false, int bufferSize = 2048)
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
            if (_buf.Length > 0 && _bufPos >= _bufLen) {
                _bufLen = BaseStream.Read(_buf);
                _bufPos = 0;

                if (_bufLen == 0) {
                    throw new EndOfStreamException();
                }
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
            FillBuffer();
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

        /// <summary> Reads a null-terminated UTF8 string. </summary>
        public string ReadNulString()
        {
            Span<byte> buf = stackalloc byte[128];
            int pos = 0;

            if (_buf.Length < 16) {
                //buffer is too small to read in chunks
                while (true) {
                    byte b = ReadByte();
                    if (b == 0) break;
                    EnsureCapacity(ref buf, pos, 1);
                    buf[pos++] = b;
                }
            } else {
                while (true) {
                    FillBuffer();
                    int bufRem = _bufLen - _bufPos;
                    int endIndex = _buf.AsSpan(_bufPos, bufRem).IndexOf((byte)0);
                    bool lastChunk = endIndex >= 0;
                    int chunkLen = lastChunk ? endIndex : bufRem;
                    var chunk = _buf.AsSpan(_bufPos, chunkLen);
                    _bufPos += chunkLen + (lastChunk ? 1 : 0);

                    if (lastChunk && pos == 0) {
                        //whole string in buffer, no need of an extra copy
                        buf = chunk;
                        pos = chunkLen;
                        break;
                    }
                    EnsureCapacity(ref buf, pos, chunkLen);
                    chunk.CopyTo(buf[pos..]);
                    pos += chunkLen;
                    if (lastChunk) break;
                }
            }
            return Encoding.UTF8.GetString(buf[0..pos]);

            static void EnsureCapacity(ref Span<byte> buf, int pos, int minCount)
            {
                if (pos + minCount > buf.Length) {
                    var newBuf = new byte[Math.Max(buf.Length + minCount + 64, buf.Length * 2)];
                    buf.CopyTo(newBuf);
                    buf = newBuf;
                }
            }
        }

        public void SkipBytes(int count)
        {
            if (BaseStream.CanSeek) {
                Position += count;
            } else {
                int bufAvail = Math.Min(count, _bufLen - _bufPos);
                if (bufAvail > 0) {
                    _bufPos += bufAvail;
                    count -= bufAvail;
                }
                if (count <= 0) return;

                var buf = _buf.Length > 512 ? _buf : stackalloc byte[2048];
                while (count > 0) {
                    int blockSize = Math.Min(buf.Length, count);
                    int bytesRead = BaseStream.Read(buf[0..blockSize]);
                    if (bytesRead <= 0) {
                        throw new EndOfStreamException();
                    }
                    count -= bytesRead;
                }
            }
        }

        /// <summary> Returns a subview of the current reader. </summary>
        /// <remarks> This reader should not be used until the returned object is disposed. </remarks>
        public DataReader Slice(int length)
        {
            Stream stream;
            if (BaseStream is StreamWrapper sw) {
                //avoid nesting streams, each level could have a buffer copy
                if (length > sw._remainingBytes) {
                    throw new EndOfStreamException();
                }
                sw._remainingBytes -= length;
                stream = sw._dr.AsStream(length);
            } else {
                stream = AsStream(length);
            }
            return new DataReader(stream, false);
        }
    }
}