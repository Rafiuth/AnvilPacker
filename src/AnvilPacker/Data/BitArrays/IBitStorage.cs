using System;
using System.Runtime.CompilerServices;
using AnvilPacker.Level;

namespace AnvilPacker.Data
{
    public interface IBitStorage
    {
        long[] Data { get; }
        int Count { get; }
        int BitsPerElement { get; }
        
        int this[int index] { get; set; }

        /// <summary> Reads all elements in this storage using <see cref="IBitStorageVisitor.Use(int, int)"/> as the callback. </summary>
        void Unpack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor;

        /// <summary> Populates this storage with elements generated by <see cref="IBitStorageVisitor.Create(int)"/></summary>
        void Pack<TVisitor>(TVisitor visitor) where TVisitor : IBitStorageVisitor;

        void Unpack<T>(T[] dest) where T : struct
        {
            Unpack(new ArrayStorageVisitor<T>() {
                Array = dest
            });
        }
        void Pack<T>(T[] src) where T : struct
        {
            Pack(new ArrayStorageVisitor<T>() {
                Array = src
            });
        }
    }
    public interface IBitStorageVisitor
    {
        void Use(int index, int value);
        int Create(int index);
    }
    public struct ArrayStorageVisitor<TElem> : IBitStorageVisitor
        where TElem : struct
    {
        public TElem[] Array;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Use(int index, int value)
        {
            if (typeof(TElem) == typeof(int)) {
                Array[index] = (TElem)(object)(int)value;
                return;
            }
            if (typeof(TElem) == typeof(short)) {
                Array[index] = (TElem)(object)(short)value;
                return;
            }
            if (typeof(TElem) == typeof(ushort)) {
                Array[index] = (TElem)(object)(ushort)value;
                return;
            }
            if (typeof(TElem) == typeof(BlockId)) {
                Array[index] = (TElem)(object)(BlockId)value;
                return;
            }
            throw new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Create(int index)
        {
            if (typeof(TElem) == typeof(int)) {
                return (int)(object)Array[index];
            }
            if (typeof(TElem) == typeof(short)) {
                return (int)(short)(object)Array[index];
            }
            if (typeof(TElem) == typeof(ushort)) {
                return (int)(ushort)(object)Array[index];
            }
            if (typeof(TElem) == typeof(BlockId)) {
                return (int)(BlockId)(object)Array[index];
            }
            throw new NotSupportedException();
        }
    }
}