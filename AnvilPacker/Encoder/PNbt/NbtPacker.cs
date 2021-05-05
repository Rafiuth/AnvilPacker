#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.Pnbt
{
    //http://stevehanov.ca/blog/?id=104
    //http://stevehanov.ca/blog/cjson.js
    //https://github.com/That3Percent/tree-buf
    public class NbtPacker
    {
        private List<CompoundTag> _tags = new();
        private Node _root = new Node(null, new SchemaField("root", TagType.End));
        private StringPool _stringPool = new();

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

        private List<Schema> CreateSchemas()
        {
            var schemas = new List<Schema>();
            var queue = new Queue<Node>();

            _root.Schema = new Schema() { Id = 0 };
            schemas.Add(_root.Schema);
            
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
                    schema.Id = schemas.Count;

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

            dw.WriteByte(1); //version
            _stringPool.WriteTable(dw);

            dw.WriteVarUInt(schemas.Count);
            foreach (var schema in schemas) {
                schema.Write(dw);
            }

            dw.WriteVarUInt(_tags.Count);
            foreach (var tag in _tags) {
                WriteCompound(dw, tag);
            }

            _tags.Clear();
            if (reset) {
                _stringPool.Clear();
                _root = new Node(null, new SchemaField("", TagType.End));
            }
        }

        private void WriteCompound(DataWriter dw, CompoundTag tag)
        {
            var schema = FindSchema(tag);

            dw.WriteVarUInt(schema.Id);
            while (schema != null) {
                foreach (var field in schema.Fields) {
                    WriteTagField(dw, field.Data!, tag[field.Name]);
                }
                schema = schema.Parent;
            }
        }
        private void WriteTagField(DataWriter dw, FieldData? opaqueData, NbtTag tag)
        {
            void WritePrim<T>() where T : unmanaged
            {
                var val = ((PrimitiveTag<T>)tag).Value;
                dw.WriteLE<T>(val);
            }
            void WriteArr<T>() where T : unmanaged
            {
                var arr = ((PrimitiveTag<T[]>)tag).Value;
                var data = (FieldArrayData)opaqueData!;

                data.Len.Write(dw, arr.Length);
                dw.WriteBulkLE<T>(arr);
            }

            switch (tag.Type) {
                case TagType.Byte:
                case TagType.Short:
                case TagType.Int:
                case TagType.Long: {
                    var data = (FieldIntData)opaqueData!;
                    data.Write(dw, tag.Value<long>());
                    break;
                }
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
                    var data = (FieldListData)opaqueData!;

                    data.Len.Write(dw, list.Count);
                    if (list.Count == 0) break;

                    Ensure.That(data.ElemSchema == null); //not supported yet

                    if (data.ElemType == TagType.End) {
                        dw.WriteByte((byte)list.ElementType);
                    }
                    foreach (var elem in list) {
                        WriteTagField(dw, data.ElemData, elem);
                    }
                    break;
                }
                case TagType.Compound: {
                    WriteCompound(dw, (CompoundTag)tag);
                    break;
                }
                default: throw new NotSupportedException("Unknown tag type");
            }
        }

        private Schema FindSchema(CompoundTag tag)
        {
            var node = _root;
            foreach (var (k, v) in tag) {
                node = node.GetChildren(k, v);
                Process(v);
            }
            return node.Schema ?? throw new InvalidOperationException();
        }

        private class Node
        {
            public Node? Parent;
            public SchemaField Field;
            public Dictionary<SchemaField, Node> Children = new();
            public List<NbtTag> Links = new();
            public Schema? Schema = null;

            public Node(Node? parent, SchemaField field)
            {
                Parent = parent;
                Field = field;
            }

            public Node GetChildren(string name, NbtTag tag)
            {
                var field = new SchemaField(name, tag.Type);

                if (!Children.TryGetValue(field, out var node)) {
                    node = new Node(this, field);
                    Children.Add(field, node);
                }
                FieldData.Merge(ref node.Field.Data, tag);
                return node;
            }
        }
    }
}