using System;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary> Big-endian binary primitive writer </summary>
    public abstract class DataWriter : IDisposable
    {
        public abstract long Position { get; set; }

        /// <summary> Writes a primitive value (size &lt;= 8), in big endian order to the output. </summary>
        public virtual void Write<T>(T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) {
                value = Mem.BSwap(value);
            }
            WriteBytes(Mem.CreateSpan<T, byte>(ref value, 1));
        }
        public abstract void WriteBytes(ReadOnlySpan<byte> buf);

        public abstract void Dispose();

        public void WriteByte(int v)    => Write((byte)v);
        public void WriteShort(int v)   => Write((short)v);
        public void WriteInt(int v)     => Write((int)v);
        public void WriteLong(long v)   => Write((long)v);

        //using casts to reduce generic overloading bloat
        public void WriteSByte(int v)       => Write((byte)v);
        public void WriteUShort(int v)      => Write((short)v);
        public void WriteUInt(uint v)       => Write((int)v);
        public void WriteULong(ulong v)     => Write((long)v);
        public void WriteFloat(float v)     => Write(BitConverter.SingleToInt32Bits(v));
        public void WriteDouble(double v)   => Write(BitConverter.DoubleToInt64Bits(v));

        /// <summary> Writes an UTF8 string prefixed with a ushort indicating its length </summary>
        public virtual void WriteString(string v)
        {
            var enc = Encoding.UTF8;
            int len = enc.GetByteCount(v);
            if (len > 65535) {
                throw new InvalidOperationException("String cannot be larger than 65535 characters.");
            }
            var buf = len < 256 ? stackalloc byte[len] : new byte[len];

            enc.GetBytes(v, buf);

            WriteUShort((ushort)len);
            WriteBytes(buf);
        }
    }
}
