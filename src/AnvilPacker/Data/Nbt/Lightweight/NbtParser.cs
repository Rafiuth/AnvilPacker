using AnvilPacker.Util;

namespace AnvilPacker.Data.Nbt
{
    internal unsafe struct NbtParser
    {
        private ArrayReader _reader;

        private NbtDocument _doc;
        private List<object> _meta;

        public NbtDocument Parse(byte[] data, int offset, bool copy, out int bytesRead)
        {
            _reader = new ArrayReader(data, offset, copy);

            var rootType = _reader.Read<TagType>();
            _reader.ReadSlice(_reader.Read<ushort>()); //name

            if (rootType != TagType.Compound) {
                throw new InvalidDataException("Root must be a compound tag.");
            }

            _meta = new List<object>(32);

            _doc = new NbtDocument();
            _doc._data = data; //init before read to improve debugging
            
            _doc.Root = ReadCompound(0);
            _doc._meta = _meta.ToArray();

            if (copy) {
                _doc._data = data[offset.._reader.Position];
            }
            bytesRead = _reader.Position;
            return _doc;
        }

        private NbtToken Read(TagType type, int depth)
        {
            if (depth++ > 256) {
                //avoid crashing with stack overflows
                throw new NotSupportedException("Too many nested tags.");
            }
            switch (type) {
                case TagType.Byte:      return ReadPrim<byte>(type);
                case TagType.Short:     return ReadPrim<short>(type);
                case TagType.Int:       return ReadPrim<int>(type);
                case TagType.Long:      return ReadPrim<long>(type);
                case TagType.Float:     return ReadPrim<float>(type);
                case TagType.Double:    return ReadPrim<double>(type);
                case TagType.ByteArray: return ReadArr<byte>(type);
                case TagType.IntArray:  return ReadArr<int>(type);
                case TagType.LongArray: return ReadArr<long>(type);
                case TagType.String:    return ReadString();
                case TagType.List:      return ReadList(depth);
                case TagType.Compound:  return ReadCompound(depth);
                default: throw new ArgumentException($"Unknown tag type: {type}");
            }
        }

        private NbtToken ReadPrim<T>(TagType type) where T : unmanaged
        {
            int pos = _reader.Position;

            _reader.ReadSlice(sizeof(T));

            return new NbtToken(_doc, type, pos);
        }
        private NbtToken ReadArr<T>(TagType type) where T : unmanaged
        {
            //Array contents are lazily bswapped by AsArray()
            //The last bit of count is used as the "initialized" flag.
            int pos = _reader.Position;

            int count = _reader.Read<int>();
            _reader.ReadSlice(count * sizeof(T));

            return new NbtToken(_doc, type, pos);
        }

        private NbtToken ReadString()
        {
            int pos = _reader.Position;

            ushort len = _reader.Read<ushort>();
            _reader.ReadSlice(len);

            return new NbtToken(_doc, TagType.String, pos);
        }
        private NbtToken ReadList(int depth)
        {
            var elemType = _reader.Read<TagType>();
            int count = _reader.Read<int>();

            var offsets = new int[count + 1];
            offsets[0] = (int)elemType;

            int metaSlot = AddMeta(offsets);

            for (int i = 0; i < count; i++) {
                var elem = Read(elemType, depth);
                offsets[i + 1] = elem._pos;
            }
            return new NbtToken(_doc, TagType.List, metaSlot);
        }
        private NbtToken ReadCompound(int depth)
        {
            var offsets = new List<CompoundProp>(16);

            while (true) {
                var type = _reader.Read<TagType>();
                if (type == TagType.End) break;

                var name = ReadString();
                var child = Read(type, depth);

                offsets.Add(new CompoundProp() {
                    Type = type,
                    NamePos = name._pos,
                    ChildPos = child._pos
                });
            }
            int metaSlot = AddMeta(offsets.ToArray());

            return new NbtToken(_doc, TagType.Compound, metaSlot);
        }

        private int AddMeta(object obj)
        {
            _meta.Add(obj);
            return _meta.Count - 1;
        }
    }

    internal struct ArrayReader
    {
        private readonly byte[] _data;
        private readonly int _posOffset;
        private int _pos;

        public int Position => _pos - _posOffset;

        public ArrayReader(byte[] data, int offset, bool subOffsetFromPos)
        {
            _data = data;
            _posOffset = subOffsetFromPos ? offset : 0;
            _pos = offset;
        }

        /// <summary> Reads a binary big-endian primitive from the buffer. </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            if (_pos + sizeof(T) > _data.Length) {
                throw new EndOfStreamException();
            }
            int start = _pos;
            _pos += sizeof(T);
            return Mem.ReadBE<T>(_data, start);
        }

        public void ReadBytes(Span<byte> dest)
        {
            _data.AsSpan(_pos, dest.Length).CopyTo(dest);
            _pos += dest.Length;
        }

        public ReadOnlySpan<byte> ReadSlice(int count)
        {
            var span = _data.AsSpan(_pos, count);
            _pos += count;
            return span;
        }
    }
}