using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Nbt;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.PNbt
{
    //http://stevehanov.ca/blog/?id=104
    //http://stevehanov.ca/blog/cjson.js
    //https://github.com/That3Percent/tree-buf
    //TODO: Planar data fields like in tree-buf
    public class NbtPacker
    {
        private List<CompoundTag> _tags = new();
        private Node _root = new Node(null, new SchemaField("root", TagType.End));
        private StringPool _stringPool = new();
        internal List<Schema>? _lastSchemas = null;

        public void Add(CompoundTag tag)
        {
            _tags.Add(tag);
            Process(tag);
        }
        
        //Traverses the tag, creates tree branches and compute other data.
        private void Process(NbtTag tag)
        {
            switch (tag.Type) {
                case TagType.Compound: {
                    var obj = (CompoundTag)tag;
                    var node = _root;
                    foreach (var (k, v) in obj) {
                        node = node.GetChildren(k, v);
                        Process(v);
                    }
                    node.Links.Add(tag);
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;
                    if (list.ElementType is not TagType.Compound or TagType.List) break;
                    foreach (var elem in list) {
                        Process(elem);
                    }
                    break;
                }
                case TagType.String: {
                    _stringPool.Add(tag.Value<string>());
                    break;
                }
            }
        }

        internal List<Schema> CreateSchemas()
        {
            var schemas = new List<Schema>();
            var queue = new Queue<Node>();

            _root.Schema = new Schema() { Id = 0 };
            
            foreach (var child in _root.Children.Values) {
                queue.Enqueue(child);
            }

            while (queue.TryDequeue(out var node)) {
                foreach (var child in node.Children.Values) {
                    queue.Enqueue(child);
                }

                if (node.Children.Count > 1 || node.Links.Count > 0) {
                    var schema = new Schema();

                    var curr = node;
                    while (curr!.Schema == null) {
                        schema.Fields.Add(curr.Field);
                        curr = curr.Parent;
                    }
                    schema.Parent = curr?.Schema;
                    schema.Id = schemas.Count + 1;

                    schemas.Add(schema);
                    node.Schema = schema;
                }
            }
            return schemas;
        }

        /// <summary> Encodes the added tags to the specified data writer, then discards previously added tags. </summary>
        /// <param name="reset">Whether to discard colleted data (schemas and constant pools). Setting to true allows for parallel decoding.</param>
        public void Encode(DataWriter dw, bool reset)
        {
            var schemas = CreateSchemas();
            _lastSchemas = schemas;

            dw.WriteByte(1); //version
            _stringPool.WriteTable(dw);

            Schema.Write(dw, schemas);

            dw.WriteVarUInt(_tags.Count);
            foreach (var tag in _tags) {
                WriteCompound(dw, tag, null);
            }

            _tags.Clear();
            if (reset) {
                _stringPool.Clear();
                _root = new Node(null, new SchemaField("", TagType.End));
            }
        }

        private void WriteCompound(DataWriter dw, CompoundTag tag, Schema? schema)
        {
            if (schema == null) {
                schema = FindSchema(tag);
                dw.WriteVarUInt(schema.Id);
            }
            while (schema != null) {
                foreach (var field in schema.Fields) {
                    WriteTag(dw, tag[field.Name], field.Data);
                }
                schema = schema.Parent;
            }
        }
        
        private void WriteTag(DataWriter dw, NbtTag tag, FieldData? opaqueData)
        {
            void WriteInt<T>() where T : unmanaged
            {
                if (opaqueData is FieldIntData data) {
                    long val = tag.Value<long>();
                    data.Write(dw, val);
                    return;
                }
                WritePrim<T>();
            }
            void WritePrim<T>() where T : unmanaged
            {
                T val = ((PrimitiveTag<T>)tag).Value;
                dw.WriteLE<T>(val);
            }
            void WriteArr<T>() where T : unmanaged
            {
                var arr = ((PrimitiveTag<T[]>)tag).Value;
                if (opaqueData is FieldArrayData data) {
                    data.Len.Write(dw, arr.Length);
                } else {
                    dw.WriteVarUInt(arr.Length);
                }
                dw.WriteBulkLE<T>(arr);
            }

            switch (tag.Type) {
                case TagType.Byte:      WriteInt<byte>(); break;
                case TagType.Short:     WriteInt<short>(); break;
                case TagType.Int:       WriteInt<int>(); break;
                case TagType.Long:      WriteInt<long>(); break;
                case TagType.Float:     WritePrim<float>(); break;
                case TagType.Double:    WritePrim<double>(); break;
                case TagType.ByteArray: WriteArr<byte>(); break;
                case TagType.IntArray:  WriteArr<int>(); break;
                case TagType.LongArray: WriteArr<long>(); break;
                case TagType.String: {
                    var str = ((PrimitiveTag<string>)tag).Value;
                    _stringPool.Write(dw, str);
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;

                    Schema? elemSchema = null;
                    FieldData? elemData = null;

                    if (opaqueData is FieldListData data && data.ElemType != TagType.End) {
                        data.Len.Write(dw, list.Count);
                        elemSchema = data.ElemSchema;
                        elemData = data.ElemData;
                    } else {
                        dw.WriteVarUInt(list.Count);
                        dw.WriteByte((byte)list.ElementType);
                    }
                    foreach (var elem in list) {
                        if (elemSchema == null) {
                            WriteTag(dw, elem, elemData);
                        } else {
                            WriteCompound(dw, (CompoundTag)elem, elemSchema);
                        }
                    }
                    break;
                }
                case TagType.Compound: {
                    var data = (FieldCompoundData?)opaqueData;
                    WriteCompound(dw, (CompoundTag)tag, data?.Type);
                    break;
                }
                default: throw new NotSupportedException("Unknown tag type");
            }
        }

        private Schema FindSchema(CompoundTag tag)
        {
            var node = _root;
            foreach (var (k, v) in tag) {
                node = node.Children[(k, v.Type)];
            }
            return node.Schema ?? throw new InvalidOperationException();
        }

        private class Node
        {
            public Node? Parent;
            public SchemaField Field;
            public Dictionary<(string Name, TagType Type), Node> Children = new();
            public List<NbtTag> Links = new();
            public Schema? Schema = null;

            public Node(Node? parent, SchemaField field)
            {
                Parent = parent;
                Field = field;
            }

            public Node GetChildren(string name, NbtTag tag)
            {
                var key = (name, tag.Type);
                if (!Children.TryGetValue(key, out var node)) {
                    node = new Node(this, new SchemaField(name, tag.Type));
                    Children.Add(key, node);
                }
                FieldData.Merge(ref node.Field.Data, tag);
                return node;
            }
        }
    }
}