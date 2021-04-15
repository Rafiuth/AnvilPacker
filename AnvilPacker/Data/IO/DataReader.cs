using System;
using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Big-endian binary primitive reader </summary>
    /// <remarks>Read() methods will throw an Exception (type depends on impl) if the end of data has been reached. </remarks>
    public abstract class DataReader : IDisposable
    {
        public abstract long Position { get; set; }

        /// <summary> Reads a primitive value (size &lt;= 8), in big endian order from the input. </summary>
        public virtual T Read<T>() where T : unmanaged
        {
            Unsafe.SkipInit(out T value);
            ReadBytes(Mem.CreateSpan<T, byte>(ref value, 1));
            if (BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            return value;
        }
        public abstract void ReadBytes(Span<byte> dest);

        public abstract void Dispose();

        public byte ReadByte()      => Read<byte>();
        public short ReadShort()    => Read<short>();
        public int ReadInt()        => Read<int>();
        public long ReadLong()      => Read<long>();

        //using casts to reduce generic overloading bloat
        public sbyte ReadSByte()    => (sbyte)Read<byte>();
        public ushort ReadUShort()  => (ushort)Read<short>();
        public uint ReadUInt()      => (uint)Read<int>();
        public ulong ReadULong()    => (ulong)Read<long>();
        public float ReadFloat()    => BitConverter.Int32BitsToSingle(Read<int>());
        public double ReadDouble()  => BitConverter.Int64BitsToDouble(Read<long>());

        public byte[] ReadBytes(int count)
        {
            var buf = new byte[count];
            ReadBytes(buf);
            return buf;
        }

        /// <summary> Reads an UTF8 string prefixed with a ushort indicating its length </summary>
        public string ReadString() => ReadString(ReadUShort());

        /// <summary> Reads an UTF8 of <paramref name="len"/> bytes. </summary>
        public virtual string ReadString(int len)
        {
            Span<byte> buf = len <= 256 ? stackalloc byte[len] : new byte[len];

            ReadBytes(buf.Slice(0, len));
            return Encoding.UTF8.GetString(buf.Slice(0, len));
        }
    }
}