#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.Pnbt
{
    public class Schema
    {
        public HashSet<SchemaField> Fields = new();
        public Schema? Parent = null;
        public int Id;

        public void Write(DataWriter dw)
        {
            dw.WriteVarUInt(Id);
            dw.WriteVarUInt(Parent?.Id ?? 0);

            dw.WriteVarUInt(Fields.Count);
            foreach (var field in Fields) {
                dw.WriteString(field.Name, dw.WriteVarUInt);
                dw.WriteByte((int)field.Type | (field.Data != null ? 0x80 : 0x00));
                
                if (field.Data != null) {
                    field.Data.UpdateLast();
                    WriteData(dw, field.Data, field.Type);
                }
            }
        }
        private void WriteData(DataWriter dw, FieldData opaqueData, TagType type)
        {
            switch (type) {
                case TagType.Byte:
                case TagType.Short:
                case TagType.Int:
                case TagType.Long: {
                    var data = (FieldIntData)opaqueData;

                    dw.WriteByte((byte)data.Size);
                    break;
                }
                case TagType.List: {
                    var data = (FieldListData)opaqueData;

                    dw.WriteByte((byte)data.ElemType);
                    dw.WriteVarUInt(data.ElemSchema?.Id ?? 0);
                    dw.WriteVarUInt((int)data.Len.Min);
                    dw.WriteVarUInt((int)data.Len.Max);
                    break;
                }
                case TagType.Compound: {
                    var data = (FieldCompoundData)opaqueData;

                    dw.WriteVarUInt(data.Type?.Id ?? 0);
                    break;
                }
                case TagType.ByteArray:
                case TagType.IntArray:
                case TagType.LongArray: {
                    var data = (FieldArrayData)opaqueData;

                    dw.WriteVarUInt((int)data.Len.Min);
                    dw.WriteVarUInt((int)data.Len.Max);
                    break;
                }
            }
        }

        public override string ToString()
        {
            var fields = string.Join(", ", Fields.Select(v => $"{v.Name}: {v.Type}"));
            return $"Schema {Id} {(Parent == null ? "" : ": " + Parent.Id)} {{ {fields} }}";
        }
    }
    public record SchemaField(
        string Name,
        TagType Type
    )
    {
        public FieldData? Data;

        public virtual bool Equals(SchemaField? other)
        {
            return other != null &&
                   other.Name == Name &&
                   other.Type == Type;
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode() * 7 + Type.GetHashCode();
        }
    }

    public abstract class FieldData
    {
        /// <summary> Update internal values before writting tags. </summary>
        public virtual void UpdateLast() { }

        public static void Merge(ref FieldData? opaqueData, NbtTag tag)
        {
            switch (tag.Type) {
                case TagType.Byte:
                case TagType.Short:
                case TagType.Int:
                case TagType.Long: {
                    var data = (FieldIntData)(opaqueData ??= new FieldIntData());
                    data.Update(tag.Value<long>());
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;
                    var data = (FieldListData)(opaqueData ??= new FieldListData());

                    if (list.Count > 0) {
                        Ensure.That(list.ElementType != TagType.End);

                        if (data.ElemType == TagType.End && !data.MixedElemTypes) {
                            data.ElemType = list.ElementType;
                            foreach (var elem in list) {
                                Merge(ref data.ElemData, elem);
                            }
                        } else if (data.ElemType != list.ElementType) {
                            data.ElemType = TagType.End;
                            data.MixedElemTypes = true;
                        }
                    }
                    //TODO: update data.ElemSchema
                    data.Len.Update(list.Count);
                    break;
                }
                case TagType.ByteArray:
                case TagType.IntArray:
                case TagType.LongArray: {
                    var arr = tag.Value<Array>();
                    var data = (FieldArrayData)(opaqueData ??= new FieldArrayData());

                    data.Len.Update(arr.Length);
                    break;
                }
            }
        }
    }

    public class FieldIntData : FieldData
    {
        public long Min = long.MaxValue;
        public long Max = long.MinValue;
        public IntSize Size;

        public void Update(long value)
        {
            Min = Math.Min(Min, value);
            Max = Math.Max(Max, value);
        }

        public override void UpdateLast()
        {
            //TODO: maybe better sizes could be picked by keeping a histogram.
            foreach (int w in new[] { 8, 16, 56 }) {
                long uRange = 1L << w;
                long sRange = 1L << (w - 1);

                if (Min >= 0 && Max < uRange) {
                    Size = IntSizes.ForWidth(w, false);
                    return;
                }
                if (Min >= -sRange && Max < sRange) {
                    Size = IntSizes.ForWidth(w, true);
                    return;
                }
            }
            Size = Min < 0 ? IntSize.SVar : IntSize.UVar;
        }

        public void Write(DataWriter dw, long value)
        {
            if (Min == Max) return;

            switch (Size) {
                case IntSize.SVar: dw.WriteVarLong(value); break;
                case IntSize.UVar: dw.WriteVarULong(value); break;
                default: WriteFixedInt(dw, value, Size.ValueBytes()); break;
            }
        }
        private static void WriteFixedInt(DataWriter dw, long value, int len)
        {
            for (int i = 0; i < len; i++) {
                dw.WriteByte((byte)value);
                value >>= 8;
            }
        }
    }
    public class FieldListData : FieldData
    {
        public TagType ElemType = TagType.End; //When decoding, this will be End for mixed element types.
        public Schema? ElemSchema = null;
        public bool MixedElemTypes = false;
        public FieldIntData Len = new();
        public FieldData? ElemData = null;
    }
    public class FieldArrayData : FieldData
    {
        public FieldIntData Len = new();
    }
    public class FieldCompoundData : FieldData
    {
        public Schema? Type = null;
    }

    public enum IntSize
    {
        Invalid = -1,
        U8,  S8,
        U16, S16,
        U24, S24,
        U32, S32,
        U40, S40,
        U48, S48,
        U56, S56,
        U64, S64,
        UVar, SVar,
    }
    public static class IntSizes
    {
        public static IntSize ForWidth(int bits, bool signed)
        {
            Ensure.That(bits >= 8 && bits <= 64 && bits % 8 == 0);

            return (IntSize)(
                (bits / 8 - 1) << 1 |
                (signed ? 1 : 0)
            );
        }
        public static int ValueBytes(this IntSize size)
        {
            return (int)size >> 1;
        }
        public static bool IsSigned(this IntSize size)
        {
            return ((int)size & 1) != 0;
        }
        public static IntSize WithSign(this IntSize size, bool signed)
        {
            int i = (int)size;
            return (IntSize)(signed ? (i | 1) : (i & ~1));
        }
    }
}