using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Data
{
    public class NibbleArray : IEnumerable<int>
    {
        public readonly byte[] Data;
        public readonly int Length;

        public int this[int index]
        {
            get => Get(Data, index);
            set => Set(Data, index, value);
        }

        public NibbleArray(byte[] data)
        {
            Data = data;
            Length = data.Length * 2;
        }
        public NibbleArray(int length)
        {
            Data = new byte[(length + 1) / 2];
            Length = length;
        }

        /// <summary> Reads a nibble from the array at the specified position. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Get(byte[] data, int index)
        {
            //byte b = arr[idx / 2];
            //return i % 2 == 0 ? b & 15 : b >> 4;
            byte b = data[index >> 1];
            int s = (index & 1) * 4;
            return (b >> s) & 15;
        }
        /// <summary> Writes a nibble to the array at the specified position. </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(byte[] data, int index, int value)
        {
            ref byte b = ref data[index >> 1];
            int s = (index & 1) * 4;
            int m = 0xF0 >> s;
            b = (byte)((b & m) | (value & 15) << s);
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < Length; i++) {
                yield return this[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
