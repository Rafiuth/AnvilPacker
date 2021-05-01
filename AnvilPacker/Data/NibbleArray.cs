#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace AnvilPacker.Data
{
    public class NibbleArray : IEnumerable<int>
    {
        public readonly byte[] Data;
        public readonly int Length;

        public int this[int index]
        {
            get {
                //byte b = arr[idx / 2];
                //return i % 2 == 0 ? b & 15 : b >> 4;
                byte b = Data[index >> 1];
                int s = (index & 1) * 4;
                return (b >> s) & 15;
            }
            set {
                ref byte b = ref Data[index >> 1];
                int s = (index & 1) * 4;
                int m = 0xF0 >> s;
                b = (byte)((b & m) | (value & 15) << s);
            }
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

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < Length; i++) {
                yield return this[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
