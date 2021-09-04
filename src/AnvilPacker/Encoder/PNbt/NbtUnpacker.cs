using System;
using AnvilPacker.Data;
using AnvilPacker.Data.Nbt;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.PNbt
{
    public class NbtUnpacker : IDisposable
    {
        private DataReader _reader;
        internal Schema[] _schemas = Array.Empty<Schema>();
        private StringPool _stringPool = new();
        private int _numTags;

        public NbtUnpacker(DataReader reader)
        {
            _reader = reader;
        }

        public void ReadHeader()
        {
            var version = _reader.ReadByte();

            _stringPool.ReadTable(_reader);
            _schemas = Schema.Read(_reader);

            _numTags = _reader.ReadVarUInt();
        }

        public CompoundTag Read()
        {
            Ensure.That(_numTags-- > 0, "End of NBT pack.");
            return ReadCompound(null);
        }

        private CompoundTag ReadCompound(Schema? schema)
        {
            schema ??= ReadSchema();
            
            var tag = new CompoundTag();
            while (schema != null) {
                foreach (var field in schema.Fields) {
                    tag[field.Name] = ReadTag(field.Type, field.Data);
                }
                schema = schema.Parent;
            }
            return tag;
        }

        private NbtTag ReadTag(TagType type, FieldData? opaqueData)
        {
            long? ReadInt()
            {
                return (opaqueData as FieldIntData)?.Read(_reader);
            }
            NbtTag ReadPrim<T>(T? existingVal = null) where T : unmanaged
            {
                T val = existingVal ?? _reader.ReadLE<T>();
                return new PrimitiveTag<T>(val);
            }
            NbtTag ReadArr<T>() where T : unmanaged
            {
                var data = (FieldArrayData?)opaqueData;
                long len = data?.Len.Read(_reader) ?? _reader.ReadVarUInt();
                var arr = GC.AllocateUninitializedArray<T>((int)len);
                _reader.ReadBulkLE<T>(arr);
                return new PrimitiveTag<T[]>(arr);
            }

            switch (type) {
                case TagType.Byte:      return ReadPrim((byte?)ReadInt());
                case TagType.Short:     return ReadPrim((short?)ReadInt());
                case TagType.Int:       return ReadPrim((int?)ReadInt());
                case TagType.Long:      return ReadPrim((long?)ReadInt());
                case TagType.Float:     return ReadPrim<float>();
                case TagType.Double:    return ReadPrim<double>();
                case TagType.ByteArray: return ReadArr<byte>();
                case TagType.IntArray:  return ReadArr<int>();
                case TagType.LongArray: return ReadArr<long>();
                case TagType.String: {
                    var str = _stringPool.Read(_reader);
                    return new PrimitiveTag<string>(str);
                }
                case TagType.List: {
                    int len;
                    TagType elemType;
                    Schema? elemSchema = null;
                    FieldData? elemData = null;

                    if (opaqueData is FieldListData data && data.ElemType != TagType.End) {
                        len = (int)data.Len.Read(_reader);
                        elemType = data.ElemType;
                        elemSchema = data.ElemSchema;
                        elemData = data.ElemData;
                    } else {
                        len = _reader.ReadVarUInt();
                        elemType = (TagType)_reader.ReadByte();
                    }
                    var list = new ListTag(len);

                    for (int i = 0; i < len; i++) {
                        var tag = 
                            elemSchema == null 
                                ? ReadTag(elemType, elemData) 
                                : ReadCompound(elemSchema);
                        list.Add(tag);
                    }
                    return list;
                }
                case TagType.Compound: {
                    var data = (FieldCompoundData?)opaqueData;
                    return ReadCompound(data?.Type);
                }
                default: throw new NotSupportedException("Unknown tag type");
            }
        }
        private Schema? ReadSchema()
        {
            int id = _reader.ReadVarUInt() - 1;
            return id < 0 ? null : _schemas[id];
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}