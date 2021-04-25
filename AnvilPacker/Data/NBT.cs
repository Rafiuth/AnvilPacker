using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace AnvilPacker.Data
{
    public abstract class NbtTag
    {
        public abstract TagType Type { get; }

        internal static NbtTag Read(TagType type, DataReader din, int depth)
        {
            if (depth++ > 256) {
                //Protect against StackOverflowException
                throw new InvalidDataException("Malformed NBT data: too many nested tags.");
            }
            static PrimitiveTag<T> NewPrim<T>(T value) => new PrimitiveTag<T>(value);

            switch (type) {
                case TagType.Byte:      return NewPrim(din.ReadByte());
                case TagType.Short:     return NewPrim(din.ReadShortBE());
                case TagType.Int:       return NewPrim(din.ReadIntBE());
                case TagType.Long:      return NewPrim(din.ReadLongBE());
                case TagType.Float:     return NewPrim(din.ReadFloatBE());
                case TagType.Double:    return NewPrim(din.ReadDoubleBE());
                case TagType.String:    return NewPrim(din.ReadString());
                case TagType.ByteArray: return NewPrim(din.ReadBytes(din.ReadIntBE()));
                case TagType.IntArray: {
                    var arr = new int[din.ReadIntBE()];
                    for (int i = 0; i < arr.Length; i++) {
                        arr[i] = din.ReadIntBE();
                    }
                    return NewPrim(arr);
                }
                case TagType.LongArray: {
                    var arr = new long[din.ReadIntBE()];
                    for (int i = 0; i < arr.Length; i++) {
                        arr[i] = din.ReadLongBE();
                    }
                    return NewPrim(arr);
                }
                case TagType.List: {
                    TagType tagType = (TagType)din.ReadByte();
                    int count = din.ReadIntBE();

                    ListTag list = new ListTag(count);
                    for (int i = 0; i < count; i++) {
                        list.Add(Read(tagType, din, depth));
                    }
                    return list;
                }
                case TagType.Compound: {
                    CompoundTag tag = new CompoundTag();
                    while (true) {
                        var tagType = (TagType)din.ReadByte();
                        if (tagType == TagType.End) {
                            return tag;
                        }
                        string name = din.ReadString();
                        tag[name] = Read(tagType, din, depth);
                    }
                }
                default: throw new ArgumentException($"Unknown tag type: {type}");
            }
        }
        internal static void Write(NbtTag tag, DataWriter dout, int depth)
        {
            if (depth++ > 256) {
                //Protect against StackOverflowException
                throw new InvalidDataException("Malformed NBT data: too many nested tags.");
            }
            T GetPrim<T>() => ((PrimitiveTag<T>)tag).Value;

            switch (tag.Type) {
                case TagType.Byte:   dout.WriteByte(GetPrim<byte>()); break;
                case TagType.Short:  dout.WriteShortBE(GetPrim<short>()); break;
                case TagType.Int:    dout.WriteIntBE(GetPrim<int>()); break;
                case TagType.Long:   dout.WriteLongBE(GetPrim<long>()); break;
                case TagType.Float:  dout.WriteFloatBE(GetPrim<float>()); break;
                case TagType.Double: dout.WriteDoubleBE(GetPrim<double>()); break;
                case TagType.String: dout.WriteString(GetPrim<string>()); break;
                case TagType.ByteArray: {
                    var arr = GetPrim<byte[]>();
                    dout.WriteIntBE(arr.Length);
                    dout.WriteBytes(arr);
                    break;
                }
                case TagType.IntArray: {
                    var arr = GetPrim<int[]>();
                    dout.WriteIntBE(arr.Length);
                    for (int i = 0; i < arr.Length; i++) {
                        dout.WriteIntBE(arr[i]);
                    }
                    break;
                }
                case TagType.LongArray: {
                    var arr = GetPrim<long[]>();
                    dout.WriteIntBE(arr.Length);
                    for (int i = 0; i < arr.Length; i++) {
                        dout.WriteLongBE(arr[i]);
                    }
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;
                    dout.WriteByte((byte)list.ElementType);
                    dout.WriteIntBE(list.Count);
                    foreach (NbtTag entry in list) {
                        Write(entry, dout, depth);
                    }
                    break;
                }
                case TagType.Compound: {
                    foreach (KeyValuePair<string, NbtTag> entry in (CompoundTag)tag) {
                        dout.WriteByte((byte)entry.Value.Type);
                        dout.WriteString(entry.Key);
                        Write(entry.Value, dout, depth);
                    }
                    dout.WriteByte((byte)TagType.End);
                    break;
                }
                default: throw new NotSupportedException("Unknown tag type");
            }
        }

        /// <summary> Gets/sets the value of a named tag, if this is a compound tag. </summary>
        /// <exception cref="InvalidOperationException">If <code>this</code> is not a <see cref="CompoundTag"/></exception>
        public virtual NbtTag this[string name]
        {
            get => throw new InvalidOperationException();
            set => throw new InvalidOperationException();
        }
        /// <summary> Gets/sets a tag at the specified index, if this is a list tag. </summary>
        /// <exception cref="InvalidOperationException">If <code>this</code> is not a <see cref="ListTag"/></exception>
        public virtual NbtTag this[int index]
        {
            get => throw new InvalidOperationException();
            set => throw new InvalidOperationException();
        }

        /// <summary> Gets/sets the value of a primitive tag. </summary>
        /// <exception cref="InvalidOperationException">If <code>this</code> is not a <see cref="PrimitiveTag"/></exception>
        public virtual T Value<T>()
        {
            throw new InvalidOperationException();
        }

        public override string ToString()
        {
            var pp = new NbtPrinter() {
                ArraySizeLimit = 128
            };
            pp.Print(this);
            return pp.ToString();
        }
    }
    public class CompoundTag : NbtTag, IEnumerable<KeyValuePair<string, NbtTag>>
    {
        private Dictionary<string, NbtTag> _tags;

        public override TagType Type => TagType.Compound;

        public int Count => _tags.Count;
        public override NbtTag this[string name]
        {
            set => _tags[name] = value;
            get => _tags.TryGetValue(name, out NbtTag tag) ? tag : null;
        }

        public CompoundTag()
        {
            _tags = new();
        }
        public CompoundTag(IEnumerable<KeyValuePair<string, NbtTag>> tags)
        {
            _tags = new(tags);
        }

        public bool ContainsKey(string key) => _tags.ContainsKey(key);
        public bool ContainsKey(string key, TagType type) => TryGetTag(key, out var tag) && tag.Type == type;

        public bool TryGetTag(string name, out NbtTag tag)
        {
            return _tags.TryGetValue(name, out tag);
        }
        public bool TryGetTag<TTag>(string name, out TTag tag) where TTag : NbtTag
        {
            if (_tags.TryGetValue(name, out var btag) && btag is TTag) {
                tag = (TTag)btag;
                return true;
            }
            tag = default;
            return false;
        }
        public bool Remove(string name)
        {
            return _tags.Remove(name);
        }

        public ListTag GetList(string name, bool returnNullIfNotFound = false)
        {
            if (_tags.TryGetValue(name, out NbtTag tag) && tag is ListTag tl) {
                return tl;
            }
            return returnNullIfNotFound ? null : new ListTag();
        }
        public CompoundTag GetCompound(string name, bool returnNullIfNotFound = false)
        {
            if (_tags.TryGetValue(name, out NbtTag tag) && tag is CompoundTag tc) {
                return tc;
            }
            return returnNullIfNotFound ? null : new CompoundTag();
        }

        public void SetList(string name, ListTag value) => _tags[name] = value;
        public void SetCompound(string name, CompoundTag value) => _tags[name] = value;

        // primitives
        public byte GetByte(string name) => Get<byte>(name);
        public short GetShort(string name) => Get<short>(name);
        public int GetInt(string name) => Get<int>(name);
        public long GetLong(string name) => Get<long>(name);
        public float GetFloat(string name) => Get<float>(name);
        public double GetDouble(string name) => Get<double>(name);
        public byte[] GetByteArray(string name) => Get<byte[]>(name);
        public string GetString(string name) => Get<string>(name);
        public int[] GetIntArray(string name) => Get<int[]>(name);
        public long[] GetLongArray(string name) => Get<long[]>(name);
        public bool GetBool(string name) => Get<byte>(name) != 0;

        public void SetByte(string name, byte value) => Set(name, value);
        public void SetShort(string name, short value) => Set(name, value);
        public void SetInt(string name, int value) => Set(name, value);
        public void SetLong(string name, long value) => Set(name, value);
        public void SetFloat(string name, float value) => Set(name, value);
        public void SetDouble(string name, double value) => Set(name, value);
        public void SetByteArray(string name, byte[] value) => Set(name, value);
        public void SetString(string name, string value) => Set(name, value);
        public void SetIntArray(string name, int[] value) => Set(name, value);
        public void SetLongArray(string name, long[] value) => Set(name, value);
        public void SetBool(string name, bool value) => Set(name, (byte)(value ? 1 : 0));

        /// <summary> Gets the value of a primitive tag or returns the default value of T if not found. </summary>
        public T Get<T>(string name)
        {
            if (_tags.TryGetValue(name, out NbtTag tag) && tag is PrimitiveTag prim) {
                return prim.Value<T>();
            }
            return default;
        }
        /// <summary> Sets the value of a tag. </summary>
        public void Set<T>(string name, T value)
        {
            if (value is NbtTag tag) {
                _tags[name] = tag;
                return;
            }

            //try update value of existing tag to avoid allocations
            if (_tags.TryGetValue(name, out tag) && tag is PrimitiveTag<T> prim) {
                prim.Value = value;
                return;
            }
            _tags[name] = PrimitiveTag.Create(value);
        }

        public IEnumerator<KeyValuePair<string, NbtTag>> GetEnumerator() => _tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _tags.GetEnumerator();
    }
    //TODO: Write a efficient generic version of this class for primitives
    //(maybe not worth because it is very uncommon for lists to store primitives)
    public class ListTag : NbtTag, IEnumerable<NbtTag>
    {
        private List<NbtTag> _tags;
        public TagType ElementType { get; private set; } = TagType.End;

        public override TagType Type => TagType.List;

        public int Count => _tags.Count;
        public override NbtTag this[int index]
        {
            get => _tags[index];
            set {
                CheckType(value);
                _tags[index] = value;
            }
        }
        public ListTag(List<NbtTag> list)
        {
            foreach (var tag in list) {
                Add(tag);
            }
        }
        public ListTag(int initialCapacity = 4)
        {
            _tags = new List<NbtTag>(initialCapacity);
        }

        public void Add<T>(T value)
        {
            if (value is not NbtTag tag) {
                tag = PrimitiveTag.Create(value);
            }
            CheckType(tag);
            _tags.Add(tag);
        }
        /// <summary> Gets the value of a PrimitiveTag at the specified index. </summary>
        public T Get<T>(int index)
        {
            if (typeof(NbtTag).IsAssignableFrom(typeof(T))) {
                return (T)(object)_tags[index];
            }
            return ((PrimitiveTag)_tags[index]).Value<T>();
        }
        public CompoundTag GetCompound(int index)
        {
            return (CompoundTag)_tags[index];
        }

        public void RemoveAt(int index)
        {
            _tags.RemoveAt(index);
        }

        private void CheckType(NbtTag value)
        {
            if (ElementType == TagType.End) {
                ElementType = value.Type;
            } else if (value.Type != ElementType) {
                throw new ArgumentException("Value type must match ElementType");
            }
        }

        public IEnumerator<NbtTag> GetEnumerator() => _tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _tags.GetEnumerator();

        public static implicit operator List<NbtTag>(ListTag tag) => tag._tags;
    }
    public abstract class PrimitiveTag : NbtTag
    {
        public static readonly IReadOnlyDictionary<Type, TagType> TypeMap = new Dictionary<Type, TagType>() {
            { typeof(byte),     TagType.Byte      },
            { typeof(short),    TagType.Short     },
            { typeof(int),      TagType.Int       },
            { typeof(long),     TagType.Long      },
            { typeof(float),    TagType.Float     },
            { typeof(double),   TagType.Double    },
            { typeof(byte[]),   TagType.ByteArray },
            { typeof(string),   TagType.String    },
            { typeof(int[]),    TagType.IntArray  },
            { typeof(long[]),   TagType.LongArray },
        };
        /// <summary> Returns the underlying tag value. </summary>
        public abstract object GetValue();

        /// <summary> Returns the underlying tag value, converted to T. </summary>
        /// <exception cref="InvalidCastException">If the value couldn't be converted to <typeparamref name="T"/></exception>
        public override T Value<T>()
        {
            if (this is PrimitiveTag<T> pt) {
                return pt.Value;
            }
            if (typeof(T) == typeof(bool))   return (T)(object)((byte)GetValue() != 0);
            if (typeof(T) == typeof(sbyte))  return (T)(object)((sbyte)(byte)GetValue());
            if (typeof(T) == typeof(ushort)) return (T)(object)((ushort)(short)GetValue());
            if (typeof(T) == typeof(uint))   return (T)(object)((uint)(int)GetValue());
            if (typeof(T) == typeof(ulong))  return (T)(object)((ulong)(long)GetValue());

            return (T)Convert.ChangeType(GetValue(), typeof(T));
        }
        /// <summary> Creates a <see cref="PrimitiveTag{T}"/></summary>
        public static PrimitiveTag Create<T>(T value)
        {
            static PrimitiveTag<TDest> Create<TDest>(T v) => new PrimitiveTag<TDest>(Unsafe.As<T, TDest>(ref v));

            if (typeof(T) == typeof(bool))   return Create<byte>(value);
            if (typeof(T) == typeof(sbyte))  return Create<byte>(value);
            if (typeof(T) == typeof(ushort)) return Create<short>(value);
            if (typeof(T) == typeof(uint))   return Create<int>(value);
            if (typeof(T) == typeof(ulong))  return Create<long>(value);

            return new PrimitiveTag<T>(value);
        }
    }
    public class PrimitiveTag<T> : PrimitiveTag
    {
        private static readonly TagType _type;
        public override TagType Type => _type;

        static PrimitiveTag()
        {
            if (!TypeMap.TryGetValue(typeof(T), out _type)) {
                throw new NotSupportedException($"PrimitiveTag of type {typeof(T)} is not directly supported. Try using PrimitiveTag.Create() instead.");
            }
        }

        public T Value;

        public PrimitiveTag(T value)
        {
            Value = value;
        }

        public static implicit operator T(PrimitiveTag<T> tag) => tag.Value;

        public override object GetValue() => Value;
    }

    public enum TagType : byte
    {
        End       = 0,
        Byte      = 1,
        Short     = 2,
        Int       = 3,
        Long      = 4,
        Float     = 5,
        Double    = 6,
        ByteArray = 7,
        String    = 8,
        List      = 9,
        Compound  = 10,
        IntArray  = 11,
        LongArray = 12
    }
    
    public class NbtIO
    {
        /// <summary> Reads a GZIP compressed tag. </summary>
        public static CompoundTag ReadCompressed(Stream input, bool leaveOpen = true)
        {
            using var dis = new DataReader(new GZipStream(input, CompressionMode.Decompress, leaveOpen));
            return Read(dis);
        }
        /// <summary> Writes a GZIP compressed tag. </summary>
        public static void WriteCompressed(CompoundTag tag, Stream output, bool leaveOpen = true)
        {
            using var dos = new DataWriter(new GZipStream(output, CompressionMode.Compress, leaveOpen));
            Write(tag, dos);
        }
        /// <summary> Reads a GZIP compressed tag. </summary>
        public static CompoundTag ReadCompressed(string filename)
        {
            using var dis = new DataReader(new GZipStream(File.OpenRead(filename), CompressionMode.Decompress, leaveOpen: false));
            return Read(dis);
        }
        /// <summary> Writes a GZIP compressed tag. </summary>
        public static void WriteCompressed(CompoundTag tag, string filename)
        {
            var tmpFilename = filename + ".tmp";

            using var dos = new DataWriter(new GZipStream(File.Create(tmpFilename), CompressionMode.Compress, leaveOpen: false));
            try {
                Write(tag, dos);
            } catch {
                File.Delete(tmpFilename);
                throw;
            }
            File.Move(tmpFilename, filename, overwrite: true);
        }

        /// <summary> Reads a GZIP compressed tag. </summary>
        public static CompoundTag Decompress(byte[] buffer)
        {
            using var dis = new DataReader(new GZipStream(new MemoryStream(buffer), CompressionMode.Decompress, false));
            return Read(dis);
        }
        /// <summary> Writes a GZIP compressed tag. </summary>
        public static byte[] Compress(CompoundTag tag)
        {
            using var mem = new MemoryStream();
            using var dos = new DataWriter(new GZipStream(mem, CompressionMode.Compress, true));
            Write(tag, dos);
            return mem.ToArray();
        }

        public static CompoundTag Read(DataReader dis)
        {
            byte type = dis.ReadByte();
            string name = dis.ReadString();
            
            if (NbtTag.Read((TagType)type, dis, 0) is CompoundTag tag) {
                return tag;
            }
            throw new IOException("Root tag must be a named compound tag");
        }
        public static void Write(CompoundTag tag, DataWriter dos)
        {
            dos.WriteByte((byte)tag.Type);
            dos.WriteString("");
            NbtTag.Write(tag, dos, 0);
        }
    }

    /// <summary> A NBT pretty printer, intended for debugging purpouses. </summary>
    public class NbtPrinter
    {
        private StringBuilder _sb;
        private int _level = 0;

        /// <summary> Truncates primitive arrays if they are larger than this value. </summary>
        public int ArraySizeLimit { get; set; } = int.MaxValue;

        public NbtPrinter()
            : this(new StringBuilder())
        {
        }
        public NbtPrinter(StringBuilder sb)
        {
            _sb = sb;
        }

        private void Append(string name, string content)
        {
            Indent();
            _sb.AppendFormat("{0}{1}\n", name, content);
        }
        private void Begin(string name)
        {
            Indent();
            _sb.Append(name + " {\n");
            _level++;
        }
        private void End()
        {
            _level--;
            Indent();
            _sb.Append("}\n");
        }
        private void Indent()
        {
            _sb.Append(' ', _level * 4);
        }

        public void Print(NbtTag tag, string name = null)
        {
            switch (tag.Type) {
                case TagType.Byte:
                case TagType.Short:
                case TagType.Int:
                case TagType.Long:
                case TagType.Float:
                case TagType.Double:
                    Append($"{tag.Type}('{name}'): ", ((PrimitiveTag)tag).GetValue().ToString());
                    break;
                case TagType.String: {
                    var str = (string)((PrimitiveTag)tag).GetValue();
                    Append($"String('{name}'): ", "\"" + str.Replace("\"", "\\\"") + "\"");
                    break;
                }
                case TagType.ByteArray: {
                    PrintArray<byte>("Byte", name, tag);
                    break;
                }
                case TagType.IntArray: {
                    PrintArray<int>("Int", name, tag);
                    break;
                }
                case TagType.LongArray: {
                    PrintArray<long>("Long", name, tag);
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;
                    Begin($"List<{list.ElementType}>('{name}'):");
                    for (int i = 0; i < list.Count; i++) {
                        Print(list[i]);
                    }
                    End();
                    break;
                }
                case TagType.Compound: {
                    var comp = (CompoundTag)tag;
                    Begin($"Compound('{name}'):");
                    foreach (var kv in comp) {
                        Print(kv.Value, kv.Key);
                    }
                    End();
                    break;
                }
                default:
                    throw new NotImplementedException($"Printer for {tag.Type} ({tag.GetType()})");
            }
        }

        private void PrintArray<T>(string type, string name, NbtTag tag)
        {
            var arr = (T[])((PrimitiveTag)tag).GetValue();

            Indent();
            _sb.Append($"{type}[{arr.Length}]('{name}'): ");

            int len = Math.Min(arr.Length, ArraySizeLimit);
            for (int i = 0; i < len; i++) {
                if (i != 0) _sb.Append(' ');
                _sb.Append(arr[i]);
            }
            if (arr.Length > ArraySizeLimit) {
                _sb.Append("...");
            }
            _sb.Append('\n');
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}
