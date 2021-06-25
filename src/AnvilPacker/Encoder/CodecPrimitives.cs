using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public static class CodecPrimitives
    {
        public static void WriteVarUInt(this DataWriter dw, int val)
        {
            while ((val & ~0x7F) != 0) {
                dw.WriteByte((byte)val | 0x80);
                val = (int)((uint)val >> 7);
            }
            dw.WriteByte(val);
        }
        public static int ReadVarUInt(this DataReader dr)
        {
            int val = 0;
            int shift = 0;
            while (shift < 32) {
                byte b = dr.ReadByte();
                val |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) {
                    return val;
                }
                shift += 7;
            }
            throw new FormatException("Corrupted VarInt");
        }

        public static void WriteVarInt(this DataWriter dw, int val)
        {
            //See https://developers.google.com/protocol-buffers/docs/encoding#signed_integers
            dw.WriteVarUInt((val << 1) ^ (val >> 31));
        }
        public static int ReadVarInt(this DataReader dr)
        {
            var val = dr.ReadVarUInt();
            return (int)((uint)val >> 1) ^ -(val & 1);
        }

        public static void WriteVarULong(this DataWriter dw, long val)
        {
            while ((val & ~0x7FL) != 0) {
                dw.WriteByte((byte)val | 0x80);
                val = (long)((ulong)val >> 7);
            }
            dw.WriteByte((byte)val);
        }
        public static long ReadVarULong(this DataReader dr)
        {
            long val = 0;
            int shift = 0;
            while (shift < 64) {
                byte b = dr.ReadByte();
                val |= (b & 0x7FL) << shift;
                if ((b & 0x80) == 0) {
                    return val;
                }
                shift += 7;
            }
            throw new FormatException("Corrupted VarLong");
        }
        public static void WriteVarLong(this DataWriter dw, long val)
        {
            //See https://developers.google.com/protocol-buffers/docs/encoding#signed_integers
            dw.WriteVarULong((val << 1) ^ (val >> 63));
        }
        public static long ReadVarLong(this DataReader dr)
        {
            var val = dr.ReadVarULong();
            return (long)((ulong)val >> 1) ^ -(val & 1);
        }

        public static int VarIntSize(long val)
        {
            return VarUIntSize((val << 1) ^ (val >> 63));
        }
        public static int VarUIntSize(long val)
        {
            return 1 + BitOperations.Log2((ulong)val) / 7;
        }
    }
}
