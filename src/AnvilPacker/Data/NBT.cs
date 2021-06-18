using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace AnvilPacker.Data
{
    //Note: all derived tags must have a public constructor that takes no arguments.
    public abstract class NbtTag
    {
        public abstract TagType Type { get; }

        internal static NbtTag Read(TagType type, DataReader dr, int depth)
        {
            if (depth++ > 256) {
                //prevent stack overflow crashes
                throw new InvalidDataException("Malformed NBT data: too many nested tags.");
            }
            PrimitiveTag<T> ReadPrim<T>() where T : unmanaged
            {
                var value = dr.ReadBE<T>();
                return new PrimitiveTag<T>(value);
            }
            PrimitiveTag<T[]> ReadArr<T>() where T : unmanaged
            {
                int len = dr.ReadIntBE();
                var arr = GC.AllocateUninitializedArray<T>(len);
                dr.ReadBulkBE<T>(arr);
                return new PrimitiveTag<T[]>(arr);
            }

            switch (type) {
                case TagType.Byte:      return ReadPrim<byte>();
                case TagType.Short:     return ReadPrim<short>();
                case TagType.Int:       return ReadPrim<int>();
                case TagType.Long:      return ReadPrim<long>();
                case TagType.Float:     return ReadPrim<float>();
                case TagType.Double:    return ReadPrim<double>();
                case TagType.ByteArray: return ReadArr<byte>();
                case TagType.IntArray:  return ReadArr<int>();
                case TagType.LongArray: return ReadArr<long>();
                case TagType.String:    return new PrimitiveTag<string>(dr.ReadUTF());
                case TagType.List: {
                    var elemType = (TagType)dr.ReadByte();
                    int count = dr.ReadIntBE();

                    var list = new ListTag(count);
                    for (int i = 0; i < count; i++) {
                        list.Add(Read(elemType, dr, depth));
                    }
                    return list;
                }
                case TagType.Compound: {
                    var tag = new CompoundTag();
                    while (true) {
                        var childType = (TagType)dr.ReadByte();
                        if (childType == TagType.End) {
                            return tag;
                        }
                        string name = dr.ReadUTF();
                        tag[name] = Read(childType, dr, depth);
                    }
                }
                default: throw new ArgumentException($"Unknown tag type: {type}");
            }
        }
        internal static void Write(NbtTag tag, DataWriter dw, int depth)
        {
            if (depth++ > 256) {
                //prevent stack overflow crashes
                throw new InvalidDataException("Malformed NBT data: too many nested tags.");
            }
            void WritePrim<T>() where T : unmanaged
            {
                var value = ((PrimitiveTag<T>)tag).Value;
                dw.WriteBE<T>(value);
            }
            void WriteArr<T>() where T : unmanaged
            {
                var arr = ((PrimitiveTag<T[]>)tag).Value;
                dw.WriteIntBE(arr.Length);
                dw.WriteBulkBE<T>(arr);
            }

            switch (tag.Type) {
                case TagType.Byte:      WritePrim<byte>(); break;
                case TagType.Short:     WritePrim<short>(); break;
                case TagType.Int:       WritePrim<int>(); break;
                case TagType.Long:      WritePrim<long>(); break;
                case TagType.Float:     WritePrim<float>(); break;
                case TagType.Double:    WritePrim<double>(); break;
                case TagType.ByteArray: WriteArr<byte>(); break;
                case TagType.IntArray:  WriteArr<int>(); break;
                case TagType.LongArray: WriteArr<long>(); break;
                case TagType.String: {
                    var str = ((PrimitiveTag<string>)tag).Value;
                    dw.WriteUTF(str);
                    break;
                }
                case TagType.List: {
                    var list = (ListTag)tag;
                    dw.WriteByte((byte)list.ElementType);
                    dw.WriteIntBE(list.Count);
                    foreach (NbtTag entry in list) {
                        Write(entry, dw, depth);
                    }
                    break;
                }
                case TagType.Compound: {
                    foreach (var (key, val) in (CompoundTag)tag) {
                        dw.WriteByte((byte)val.Type);
                        dw.WriteUTF(key);
                        Write(val, dw, depth);
                    }
                    dw.WriteByte((byte)TagType.End);
                    break;
                }
                default: throw new NotSupportedException("Unknown tag type");
            }
        }

        /// <summary> Gets/sets the value of a named tag, if this is a compound tag. Null is returned if it doesn't exist. </summary>
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

        public override string ToString() => ToString(false);

        public string ToString(bool pretty)
        {
            var sw = new StringWriter();
            var printer = new NbtPrinter(sw) {
                Pretty = pretty
            };
            printer.Print(this);
            return sw.ToString();
        }
    }
    [DebuggerTypeProxy(typeof(DebugView))]
    [DebuggerDisplay("Count = {Count}")]
    public class CompoundTag : NbtTag, IEnumerable<KeyValuePair<string, NbtTag>>
    {
        private Dictionary<string, NbtTag> _tags;

        public override TagType Type => TagType.Compound;

        public int Count => _tags.Count;
        public override NbtTag this[string name]
        {
            get => Get<NbtTag>(name, TagGetMode.Null);
            set => Set(name, value);
        }

        public CompoundTag()
        {
            _tags = new();
        }
        public CompoundTag(IEnumerable<KeyValuePair<string, NbtTag>> tags)
        {
            _tags = new(tags);
        }

        /// <summary> Tries to gets a tag, or the value of a primitive tag. </summary>
        /// <returns> Whether the operation was successfull. </returns>
        public bool TryGet<T>(string name, out T value)
        {
            if (_tags.TryGetValue(name, out NbtTag tag)) {
                if (typeof(NbtTag).IsAssignableFrom(typeof(T))) {
                    value = (T)(object)tag;
                    return true;
                }
                if (tag is PrimitiveTag prim) {
                    value = prim.Value<T>();
                    return true;
                }
            }
            value = default;
            return false;
        }
        /// <summary>  Gets a tag, or the value of a primitive tag. </summary>
        /// <param name="mode">The action to take if the tag doesn't exist. </param>
        public T Get<T>(string name, TagGetMode mode = TagGetMode.Throw)
        {
            if (_tags.TryGetValue(name, out NbtTag tag)) {
                if (typeof(NbtTag).IsAssignableFrom(typeof(T))) {
                    return (T)(object)tag;
                }
                if (tag is PrimitiveTag prim) {
                    return prim.Value<T>();
                }
                throw new InvalidCastException($"Cannot cast tag '{name}' value from {tag.Type} to {typeof(T)}");
            }
            switch (mode) {
                default:
                case TagGetMode.Throw:
                    throw new KeyNotFoundException($"Tag '{name}' not found.");
                case TagGetMode.Null: 
                    return default;
                case TagGetMode.Create: {
                    if (!typeof(NbtTag).IsAssignableFrom(typeof(T))) {
                        throw new InvalidOperationException("Mode cannot be 'Create' for primitive values.");
                    }
                    var val = Activator.CreateInstance<T>();
                    _tags.Add(name, (NbtTag)(object)val);
                    return val;
                }
            }
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
        public bool ContainsKey(string name)
        {
            return _tags.ContainsKey(name);
        }
        public bool ContainsKey(string name, TagType type)
        {
            return _tags.TryGetValue(name, out var tag) && tag.Type == type;
        }
        public bool Remove(string name)
        {
            return _tags.Remove(name);
        }

        #region Get/Set Aliases
        public ListTag GetList(string name, TagGetMode mode = TagGetMode.Throw)
        {
            return Get<ListTag>(name, mode);
        }
        public CompoundTag GetCompound(string name, TagGetMode mode = TagGetMode.Throw)
        {
            return Get<CompoundTag>(name, mode);
        }

        public void SetList(string name, ListTag value) => _tags[name] = value;
        public void SetCompound(string name, CompoundTag value) => _tags[name] = value;

        // primitives
        public byte GetByte(string name, TagGetMode mode = TagGetMode.Throw) => Get<byte>(name, mode);
        public sbyte GetSByte(string name, TagGetMode mode = TagGetMode.Throw) => Get<sbyte>(name, mode);
        public short GetShort(string name, TagGetMode mode = TagGetMode.Throw) => Get<short>(name, mode);
        public int GetInt(string name, TagGetMode mode = TagGetMode.Throw) => Get<int>(name, mode);
        public long GetLong(string name, TagGetMode mode = TagGetMode.Throw) => Get<long>(name, mode);
        public float GetFloat(string name, TagGetMode mode = TagGetMode.Throw) => Get<float>(name, mode);
        public double GetDouble(string name, TagGetMode mode = TagGetMode.Throw) => Get<double>(name, mode);
        public byte[] GetByteArray(string name, TagGetMode mode = TagGetMode.Throw) => Get<byte[]>(name, mode);
        public string GetString(string name, TagGetMode mode = TagGetMode.Throw) => Get<string>(name, mode);
        public int[] GetIntArray(string name, TagGetMode mode = TagGetMode.Throw) => Get<int[]>(name, mode);
        public long[] GetLongArray(string name, TagGetMode mode = TagGetMode.Throw) => Get<long[]>(name, mode);
        public bool GetBool(string name, TagGetMode mode = TagGetMode.Throw) => Get<byte>(name, mode) != 0;

        public void SetByte(string name, byte value) => Set(name, value);
        public void SetSByte(string name, sbyte value) => Set(name, value);
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
        #endregion

        public IEnumerator<KeyValuePair<string, NbtTag>> GetEnumerator() => _tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _tags.GetEnumerator();

        protected class DebugView
        {
            private CompoundTag _tag;

            public DebugView(CompoundTag tag)
            {
                _tag = tag;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<string, NbtTag>[] Items
            {
                get {
                    return _tag.ToArray();
                }
            }
        }
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
        public ListTag() : this(4) { }
        public ListTag(List<NbtTag> list)
        {
            foreach (var tag in list) {
                Add(tag);
            }
        }
        public ListTag(int initialCapacity)
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
        public void AddRange(IEnumerable<NbtTag> tags)
        {
            foreach (var tag in tags) {
                Add(tag);
            }
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
        
        public void Clear()
        {
            _tags.Clear();
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
            
            if (typeof(T) == typeof(Array))  return (T)(object)GetValue();

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

        public PrimitiveTag() { }
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
    
    public enum TagGetMode
    {
        /// <summary> Throw <see cref="KeyNotFoundException"/> if the tag doesn't exist. </summary>
        Throw,
        /// <summary> Return null if the tag doesn't exist. </summary>
        Null,
        /// <summary> Create and add a new tag if it doesn't exist. </summary>
        Create
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

            try {
                using var dos = new DataWriter(new GZipStream(File.Create(tmpFilename), CompressionMode.Compress, leaveOpen: false));
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
            using (var dos = new DataWriter(new GZipStream(mem, CompressionMode.Compress, true))) {
                Write(tag, dos);
            }
            return mem.ToArray();
        }

        public static CompoundTag Read(DataReader dis)
        {
            byte type = dis.ReadByte();
            string name = dis.ReadUTF();
            
            if (NbtTag.Read((TagType)type, dis, 0) is CompoundTag tag) {
                return tag;
            }
            throw new IOException("Root tag must be a named compound tag");
        }
        public static void Write(CompoundTag tag, DataWriter dos)
        {
            dos.WriteByte((byte)tag.Type);
            dos.WriteUTF("");
            NbtTag.Write(tag, dos, 0);
        }
    }

    /// <summary> A NBT pretty printer, intended for debugging purpouses. </summary>
    public class NbtPrinter
    {
        private TextWriter _tw;
        private int _level = 0;
        public bool Pretty = true;

        public NbtPrinter(TextWriter tw)
        {
            _tw = tw;
        }

        private void Begin(string brace)
        {
            _tw.Write(brace);
            _level++;
        }
        private void End(string brace, bool newLine)
        {
            _level--;
            if (newLine && Pretty) {
                _tw.Write('\n');
                Indent();
            }
            _tw.Write(brace);
        }
        private void Indent()
        {
            _tw.Write(new string(' ', _level * 2));
        }

        public void Print(NbtTag tag)
        {
            switch (tag.Type) {
                case TagType.Byte:      PrintPrim(tag, "b"); break;
                case TagType.Short:     PrintPrim(tag, "s"); break;
                case TagType.Int:       PrintPrim(tag, ""); break;
                case TagType.Long:      PrintPrim(tag, "L"); break;
                case TagType.Float:     PrintPrim(tag, "f"); break;
                case TagType.Double:    PrintPrim(tag, ""); break;
                case TagType.String: {
                    var str = tag.Value<string>();
                    PrintStr(str);
                    break;
                }
                case TagType.ByteArray: PrintArray<byte>("byte", tag); break;
                case TagType.IntArray:  PrintArray<int>("int", tag); break;
                case TagType.LongArray: PrintArray<long>("long", tag); break;
                case TagType.List: {
                    var list = (ListTag)tag;
                    bool isPrim = PrimitiveTag.TypeMap.Values.Contains(list.ElementType);
                    PrintSequence("[", "]", isPrim, list, Print);
                    break;
                }
                case TagType.Compound: {
                    var comp = (CompoundTag)tag;
                    PrintSequence("{", "}", false, comp, e => {
                        PrintStr(e.Key);
                        _tw.Write(Pretty ? ": " : ":");
                        Print(e.Value);
                    });
                    break;
                }
                default:
                    throw new NotImplementedException($"Printer for {tag.Type} ({tag.GetType()})");
            }
        }

        private void PrintStr(string str)
        {
            _tw.Write('"');
            _tw.Write(str.Replace("\"", "\\\""));
            _tw.Write('"');
        }
        private void PrintPrim(NbtTag tag, string postfix)
        {
            object val = ((PrimitiveTag)tag).GetValue();
            string str = string.Format(CultureInfo.InvariantCulture, "{0}", val);

            _tw.Write(str);
            if (val is double or float && !str.Contains('.')) {
                _tw.Write(".0");
            }
            _tw.Write(postfix);
        }
        private void PrintSequence<T>(string openBrace, string closeBrace, bool isPrim, IEnumerable<T> elems, Action<T> printElem)
        {
            Begin(openBrace);

            int i = 0;
            foreach (var elem in elems) {
                _tw.Write(i == 0 ? "" : (Pretty ? ", " : ","));
                if (Pretty && (!isPrim || i % 32 == 0)) {
                    _tw.Write('\n');
                    Indent();
                }
                printElem(elem);
                i++;
            }
            End(closeBrace, i > 0);
        }
        private void PrintArray<T>(string type, NbtTag tag)
        {
            var arr = ((PrimitiveTag<T[]>)tag).Value;
            PrintSequence(type + "[", "]", true, arr, v => _tw.Write(v));
        }
    }
}
