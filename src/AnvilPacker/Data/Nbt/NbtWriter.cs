using System;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Nbt
{
    public class NbtWriter : IDisposable
    {
        private readonly DataWriter _dw;
        private TagType[] _typeStack = new TagType[32];
        private int _typeStackHead;
        private ListStackEntry[] _listStack = new ListStackEntry[32];
        private int _listStackHead;

        private bool _leaveOpen;

        public NbtWriter(DataWriter dw, bool leaveOpen = true)
        {
            Ensure.That(dw.BaseStream.CanSeek, "NbtWriter does not support non seek-able streams.");
            _dw = dw;
            _leaveOpen = leaveOpen;
        }

        /// <summary> Begins a new unnamed compound tag. Parent must be root or a list. </summary>
        public void BeginCompound()
        {
            if (_typeStackHead > 0) {
                BeginListEntry(TagType.Compound);
            } else {
                //root compound
                _dw.WriteByte((byte)TagType.Compound);
                _dw.WriteUTF("");
            }
            Push(TagType.Compound);
        }
        /// <summary> Writes the given NBT tree. Parent must be root or a list. </summary>
        public void WriteTag(NbtTag tag)
        {
            if (_typeStackHead > 0) {
                BeginListEntry(tag.Type);
            } else {
                //root compound
                Ensure.That(tag.Type == TagType.Compound, "Root tag must be a compound tag");
                _dw.WriteByte((byte)TagType.Compound);
                _dw.WriteUTF("");
            }
            NbtTag.Write(tag, _dw, 0);
        }

        /// <summary> Begins a new named compound tag. Parent must be a compound tag. </summary>
        public void BeginCompound(string name)
        {
            BeginCompoundEntry(TagType.Compound, name);
            Push(TagType.Compound);
        }
        public void EndCompound()
        {
            Pop(TagType.Compound);
            _dw.WriteByte((byte)TagType.End);
        }
        public void WriteByte(string name, int value)       => WritePrim(TagType.Byte,   name, (byte)value);
        public void WriteShort(string name, int value)      => WritePrim(TagType.Short,  name, (short)value);
        public void WriteInt(string name, int value)        => WritePrim(TagType.Int,    name, (int)value);
        public void WriteLong(string name, long value)      => WritePrim(TagType.Long,   name, (long)value);
        public void WriteFloat(string name, float value)    => WritePrim(TagType.Float,  name, (float)value);
        public void WriteDouble(string name, double value)  => WritePrim(TagType.Double, name, (double)value);
        public void WriteString(string name, string value)
        {
            BeginCompoundEntry(TagType.String, name);
            _dw.WriteUTF(value);
        }
        public void WriteArray<T>(string name, ReadOnlySpan<T> elements) where T : unmanaged
        {
            BeginCompoundEntry(GetArrayType<T>(), name);
            _dw.WriteIntBE(elements.Length);
            _dw.WriteBulkBE<T>(elements);
        }
        public void WriteArray<T>(string name, T[] elements) where T : unmanaged
        {
            WriteArray<T>(name, (ReadOnlySpan<T>)elements.AsSpan());
        }
        public void BeginList(string name, TagType elemType)
        {
            BeginCompoundEntry(TagType.List, name);
            PushList(elemType);
        }
        public void WriteTag(string name, NbtTag tag)
        {
            BeginCompoundEntry(tag.Type, name);
            NbtTag.Write(tag, _dw, 0);
        }

        private void BeginCompoundEntry(TagType type, string name)
        {
            EnsureParentType(TagType.Compound);
            _dw.WriteByte((byte)type);
            _dw.WriteUTF(name);
        }
        private void WritePrim<T>(TagType type, string name, T value) where T : unmanaged
        {
            BeginCompoundEntry(type, name);
            _dw.WriteBE<T>(value);
        }

        private void PushList(TagType elemType)
        {
            Push(TagType.List);
            _dw.WriteByte((byte)elemType);
            _dw.WriteIntBE(0);

            if (_listStackHead >= _listStack.Length) {
                Array.Resize(ref _listStack, _listStack.Length * 2);
            }
            _listStack[_listStackHead++] = new ListStackEntry() {
                ElemCount = 0,
                ElemType = elemType,
                ElemCountPos = _dw.Position - 4
            };
        }
        public void EndList()
        {
            Pop(TagType.List);

            var info = _listStack[--_listStackHead];
            if (info.ElemCount == 0) return;

            long endPos = _dw.Position;
            _dw.Position = info.ElemCountPos;
            _dw.WriteIntBE(info.ElemCount);
            _dw.Position = endPos;
        }
        public void WriteByte(int value)       => WritePrim(TagType.Byte,   (byte)value);
        public void WriteShort(int value)      => WritePrim(TagType.Short,  (short)value);
        public void WriteInt(int value)        => WritePrim(TagType.Int,    (int)value);
        public void WriteLong(long value)      => WritePrim(TagType.Long,   (long)value);
        public void WriteFloat(float value)    => WritePrim(TagType.Float,  (float)value);
        public void WriteDouble(double value)  => WritePrim(TagType.Double, (double)value);
        public void WriteString(string value)
        {
            BeginListEntry(TagType.String);
            _dw.WriteUTF(value);
        }
        public void WriteArray<T>(ReadOnlySpan<T> elements) where T : unmanaged
        {
            BeginListEntry(GetArrayType<T>());
            _dw.WriteIntBE(elements.Length);
            _dw.WriteBulkBE<T>(elements);
        }
        public void WriteArray<T>(T[] elements) where T : unmanaged
        {
            WriteArray<T>((ReadOnlySpan<T>)elements.AsSpan());
        }
        public void BeginList(TagType elemType)
        {
            BeginListEntry(elemType);
            PushList(elemType);
        }

        private void BeginListEntry(TagType type)
        {
            EnsureParentType(TagType.List);
            ref var info = ref _listStack[_listStackHead - 1];
            Ensure.That(info.ElemType == type, "List elements must have the same type.");
            info.ElemCount++;
        }
        private void WritePrim<T>(TagType type, T value) where T : unmanaged
        {
            BeginListEntry(type);
            _dw.WriteBE<T>(value);
        }

        private void Push(TagType type)
        {
            if (_typeStackHead >= _typeStack.Length) {
                Array.Resize(ref _typeStack, _typeStack.Length * 2);
            }
            _typeStack[_typeStackHead++] = type;
        }
        private void Pop(TagType type)
        {
            Ensure.That(_typeStack[--_typeStackHead] == type, "Unbalanced type stack");
        }
        private void EnsureParentType(TagType type)
        {
            if (_typeStack[_typeStackHead - 1] != type) {
                throw new InvalidOperationException("This method can only be called when the parent tag type is " + type);
            }
        }
        private TagType GetArrayType<T>()
        {
                 if (typeof(T) == typeof(byte)) return TagType.ByteArray;
            else if (typeof(T) == typeof(int))  return TagType.IntArray;
            else if (typeof(T) == typeof(long)) return TagType.LongArray;
            else throw new NotSupportedException("WriteArray() only supports byte/int/long arrays.");
        }

        public void Dispose()
        {
            if (!_leaveOpen) {
                _dw.Dispose();
            }
        }

        private struct ListStackEntry
        {
            public int ElemCount;
            public TagType ElemType;
            public long ElemCountPos;
        }
    }
}