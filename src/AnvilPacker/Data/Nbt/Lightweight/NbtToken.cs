using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Nbt
{
    public struct NbtToken
    {
        internal readonly NbtDocument _doc;
        //For compound and list tags, this is a metadata slot.
        //For any other tag, this is the offset to the tag data.
        internal readonly int _pos;
        internal readonly TagType _type;

        /// <summary> Returns the type of this token. </summary>
        public TagType Type => _type;

        /// <summary> Returns the list element type. </summary>
        public TagType ElemType
        {
            get {
                CheckType(TagType.List);
                return (TagType)_doc.GetMeta<int[]>(_pos)[0];
            }
        }

        /// <summary> Returns the compound/list size. </summary>
        /// <remarks> This property is slightly expansive; always cache the result when possible. </remarks>
        public int Count
        {
            get {
                switch (Type) {
                    case TagType.Compound: {
                        return _doc.GetMeta<CompoundProp[]>(_pos).Length;
                    }
                    case TagType.List: {
                        return _doc.GetMeta<int[]>(_pos).Length - 1;
                    }
                    default: {
                        throw new InvalidOperationException($"Can't get length of tag {Type}");
                    }
                }
            }
        }

        /// <summary> Returns the child with the specified name, assuming this is a compound tag. </summary>
        public NbtToken this[string name] => GetProperty(name);

        /// <summary> Returns the element at the specified index, assuming this is a list tag. </summary>
        public NbtToken this[int index] => GetElement(index);

        /// <param name="pos">Position that the token starts, including the type. </param>
        internal NbtToken(NbtDocument doc, TagType type, int pos)
        {
            _doc = doc;
            _type = type;
            _pos = pos;
        }

        public byte     AsByte() => As<byte>  (TagType.Byte);
        public short   AsShort() => As<short> (TagType.Short);
        public int       AsInt() => As<int>   (TagType.Int);
        public long     AsLong() => As<long>  (TagType.Long);
        public float   AsFloat() => As<float> (TagType.Float);
        public double AsDouble() => As<double>(TagType.Double);
        public string AsString()
        {
            CheckType(TagType.String);
            var data = _doc._data;
            ushort len = Mem.ReadBE<ushort>(data, _pos + 0);
            return Encoding.UTF8.GetString(data, _pos + 2, len);
        }
        private unsafe T As<T>(TagType expType) where T : unmanaged
        {
            CheckType(expType);
            return Mem.ReadBE<T>(_doc._data, _pos);
        }

        public NbtArrayView<byte> AsByteArray() => AsArray<byte>(TagType.ByteArray);
        public NbtArrayView<int>  AsIntArray()  => AsArray<int> (TagType.IntArray);
        public NbtArrayView<long> AsLongArray() => AsArray<long>(TagType.LongArray);

        private unsafe NbtArrayView<T> AsArray<T>(TagType expType) where T : unmanaged
        {
            const int BSWAP_FLAG = unchecked((int)0x8000_0000);
            const int LEN_MASK   = unchecked((int)0x7FFF_FFFF);

            CheckType(expType);
            var data = _doc._data;
            int header = Mem.ReadBE<int>(data, _pos + 0);

            int len = header & LEN_MASK;
            var view = new NbtArrayView<T>(data, _pos + 4, len);

            if (BitConverter.IsLittleEndian && sizeof(T) > 1 && (header & BSWAP_FLAG) == 0) {
                Mem.BSwapBulk(view.Span);
                Mem.WriteBE<int>(data, _pos + 0, header | BSWAP_FLAG);
            }
            return view;
        }

        /// <summary> Returns the compound property with the specified name, or default if not found. </summary>
        public NbtToken GetProperty(ReadOnlySpan<byte> queryName)
        {
            var props = _doc.GetMeta<CompoundProp[]>(_pos);
            var data = _doc._data;

            foreach (var prop in props) {
                ushort nameLen = Mem.ReadBE<ushort>(data, prop.NamePos);
                if (queryName.Length != nameLen) continue;

                var name = data.AsSpan(prop.NamePos + 2, nameLen);
                if (queryName.SequenceEqual(name)) {
                    return new NbtToken(_doc, prop.Type, prop.ChildPos);
                }
            }
            return default;
        }

        /// <summary> Returns the compound property with the specified name, or default if not found. </summary>
        [SkipLocalsInit]
        public NbtToken GetProperty(string name)
        {
            var enc = Encoding.UTF8;
            Span<byte> nameUtf = stackalloc byte[256];

            if (enc.GetMaxByteCount(name.Length) < nameUtf.Length) {
                int len = enc.GetBytes(name, nameUtf);
                nameUtf = nameUtf[0..len];
            } else {
                nameUtf = enc.GetBytes(name);
            }
            return GetProperty(nameUtf);
        }
        
        /// <summary> Returns the list element at the specified index. </summary>
        public NbtToken GetElement(int index)
        {
            CheckType(TagType.List);
            var offsets = _doc.GetMeta<int[]>(_pos);
            var elemType = (TagType)offsets[0];
            int pos = offsets[(uint)index + 1]; //cast to uint and take advantage of bounds check
            return new NbtToken(_doc, elemType, pos);
        }

        /// <summary> Enumerate the properties of this compound tag. </summary>
        public IEnumerable<(string Key, NbtToken Value)> EnumerateProps()
        {
            return EnumeratePropsUtf().Select(p => {
                var keyStr = Encoding.UTF8.GetString(p.Key.Span);
                return (keyStr, p.Value);
            });
        }
        /// <summary> Enumerate the properties of this compound tag, without transcoding the names. </summary>
        public IEnumerable<(ReadOnlyMemory<byte> Key, NbtToken Value)> EnumeratePropsUtf()
        {
            var props = _doc.GetMeta<CompoundProp[]>(_pos);
            var data = _doc._data;

            foreach (var prop in props) {
                ushort nameLen = Mem.ReadBE<ushort>(data, prop.NamePos);
                var nameUtf = data.AsMemory(prop.NamePos + 2, nameLen);
                var value = new NbtToken(_doc, prop.Type, prop.ChildPos);

                yield return (nameUtf, value);
            }
        }

        /// <summary> Enumerate the elements of this list tag. </summary>
        public IEnumerable<NbtToken> EnumerateElems()
        {
            var offsets = _doc.GetMeta<int[]>(_pos);
            var elemType = (TagType)offsets[0];

            for (int i = 1; i < offsets.Length; i++) {
                yield return new NbtToken(_doc, elemType, offsets[i]);
            }
        }

        private void CheckType(TagType expType)
        {
            if (Type != expType) {
                throw new InvalidCastException();
            }
        }

        public override string ToString()
        {
            return $"Tag<{Type}>";
        }
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

    internal struct CompoundProp
    {
        public int NamePos;
        public int ChildPos;
        public TagType Type;
    }
}