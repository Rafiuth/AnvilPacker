#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.PNbt
{
    public class Schema
    {
        public HashSet<SchemaField> Fields = new();
        public Schema? Parent = null;
        public int Id;


        public static void Write(DataWriter dw, List<Schema> schemas)
        {
            dw.WriteVarUInt(schemas.Count);
            foreach (var schema in schemas) {
                schema.Write(dw);
            }
        }
        public void Write(DataWriter dw)
        {
            dw.WriteVarUInt(Id);
            dw.WriteVarUInt(Parent?.Id ?? 0);

            dw.WriteVarUInt(Fields.Count);
            foreach (var field in Fields) {
                dw.WriteString(field.Name, CodecPrimitives.WriteVarUInt);
                dw.WriteByte((int)field.Type | (field.Data != null ? 0x80 : 0x00));

                if (field.Data != null) {
                    field.Data.Prepare();
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
                    dw.WriteVarLong(data.Min);
                    dw.WriteVarLong(data.Max);
                    break;
                }
                case TagType.List: {
                    var data = (FieldListData)opaqueData;

                    dw.WriteByte((byte)data.ElemType | (data.ElemData != null ? 0x80 : 0));
                    if (data.ElemType == TagType.Compound) {
                        dw.WriteVarUInt(data.ElemSchema?.Id ?? 0);
                    }
                    if (data.ElemData != null) {
                        WriteData(dw, data.ElemData, data.ElemType);
                    }
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

        public static Schema[] Read(DataReader dr)
        {
            var schemas = new Schema[dr.ReadVarUInt()];

            for (int i = 0; i < schemas.Length; i++) {
                var s = new Schema();

                s.Id = dr.ReadVarUInt();
                s.Parent = ReadSchema(dr, schemas);

                int numFields = dr.ReadVarUInt();
                for (int fi = 0; fi < numFields; fi++) {
                    var fieldName = dr.ReadString(dr.ReadVarUInt());
                    var type = dr.ReadByte();

                    var field = new SchemaField(fieldName, (TagType)(type & 0x7F));

                    if ((type & 0x80) != 0) {
                        field.Data = ReadData(dr, field.Type, schemas);
                        field.Data?.Prepare();
                    }
                    s.Fields.Add(field);
                }

                schemas[i] = s;
            }
            FixPlaceholders(schemas);
            return schemas;
        }

        private static void FixPlaceholders(Schema[] schemas)
        {
            foreach (var schema in schemas) {
                Fix(ref schema.Parent);
                foreach (var field in schema.Fields) {
                    switch (field.Data) {
                        case FieldListData data: {
                            Fix(ref data.ElemSchema);
                            break;
                        }
                        case FieldCompoundData data: {
                            Fix(ref data.Type);
                            break;
                        }
                    }
                }
            }
            void Fix(ref Schema? p)
            {
                if (p != null && p.Id < 0) {
                    p = schemas[~p.Id];
                }
            }
        }

        private static FieldData? ReadData(DataReader dr, TagType type, Schema[] schemas)
        {
            switch (type) {
                case TagType.Byte:
                case TagType.Short:
                case TagType.Int:
                case TagType.Long: {
                    var data = new FieldIntData();
                    data.Min = dr.ReadVarLong();
                    data.Max = dr.ReadVarLong();
                    return data;
                }
                case TagType.List: {
                    var data = new FieldListData();

                    var elemTypeAndFlags = dr.ReadByte();
                    data.ElemType = (TagType)(elemTypeAndFlags & 0x7F);
                    if (data.ElemType == TagType.Compound) {
                        data.ElemSchema = ReadSchema(dr, schemas);
                    }
                    if ((elemTypeAndFlags & 0x80) != 0) {
                        data.ElemData = ReadData(dr, data.ElemType, schemas);
                    }
                    data.Len.Min = dr.ReadVarUInt();
                    data.Len.Max = dr.ReadVarUInt();
                    return data;
                }
                case TagType.Compound: {
                    var data = new FieldCompoundData();
                    data.Type = ReadSchema(dr, schemas);
                    return data;
                }
                case TagType.ByteArray:
                case TagType.IntArray:
                case TagType.LongArray: {
                    var data = new FieldArrayData();
                    data.Len.Min = dr.ReadVarUInt();
                    data.Len.Max = dr.ReadVarUInt();
                    return data;
                }
                default: return null;
            }
        }

        private static Schema? ReadSchema(DataReader dr, Schema[] schemas)
        {
            int id = dr.ReadVarUInt() - 1;
            return id < 0 ? null : 
                   schemas[id] ?? new Schema() { Id = ~id };
        }

        public override string ToString()
        {
            var fields = string.Join(", ", Fields.Select(v => $"{v.Name}: {v.Type}"));
            return $"Schema {Id}{(Parent == null ? "" : " : " + Parent.Id)} {{ {fields} }}";
        }
    }
    public record SchemaField(
        string Name,
        TagType Type
    )
    {
        public FieldData? Data;
    }

    public abstract class FieldData
    {
        /// <summary> Update internal values before reading/writing tags. </summary>
        public virtual void Prepare() { }

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

        public override void Prepare()
        {
            Size = PickSize(Min, Max);
        }
        private IntSize PickSize(long min, long max)
        {
            //TODO: maybe better sizes could be picked by keeping a histogram.
            if (InS(8))   return IntSize.S8;
            if (InU(8))   return IntSize.U8;
            if (InS(16))  return IntSize.S16;
            if (InU(16))  return IntSize.U16;
            
            if (!InS(56)) return IntSize.S64;
            
            return min < 0 ? IntSize.SVar : IntSize.UVar;

            bool InS(int bits)
            {
                long r = 1L << (bits - 1);
                return min >= -r && max < r;
            }
            bool InU(int bits)
            {
                long r = 1L << bits;
                return min >= 0 && max < r;
            }
        }

        public void Write(DataWriter dw, long value)
        {
            if (Min == Max) return;

            switch (Size) {
                case IntSize.SVar: {
                    dw.WriteVarLong(value);
                    break;
                }
                case IntSize.UVar: {
                    dw.WriteVarULong(value);
                    break;
                }
                default: {
                    int len = Size.ValueBytes();
                    while (--len >= 0) {
                        dw.WriteByte((byte)(value >> (len * 8)));
                    }
                    break;
                }
            }
        }
        
        public long Read(DataReader dr)
        {
            if (Min == Max) return Min;

            switch (Size) {
                case IntSize.SVar: {
                    return dr.ReadVarLong();
                }
                case IntSize.UVar: {
                    return dr.ReadVarULong();
                }
                default: {
                    int len = Size.ValueBytes();
                    long val = 0;
                    for (int i = 0; i < len; i++) {
                        val = (val << 8) | dr.ReadByte();
                    }
                    if (Size.IsSigned()) {
                        //sign extend
                        int shift = 64 - (len * 8);
                        val = (val << shift) >> shift;
                    }
                    return val;
                }
            }
        }
    }
    public class FieldListData : FieldData
    {
        public TagType ElemType = TagType.End; //When decoding, this will be End for mixed element types.
        public Schema? ElemSchema = null; //Schema if ElemType == Compound
        public bool MixedElemTypes = false;
        public FieldIntData Len = new();
        public FieldData? ElemData = null;

        public override void Prepare()
        {
            Len.Prepare();
            ElemData?.Prepare();
        }
    }
    public class FieldArrayData : FieldData
    {
        public FieldIntData Len = new();

        public override void Prepare()
        {
            Len.Prepare();
        }
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
            return ((int)size >> 1) + 1;
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