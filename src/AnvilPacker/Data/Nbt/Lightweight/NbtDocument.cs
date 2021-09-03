using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace AnvilPacker.Data.Nbt
{
    public class NbtDocument
    {
        internal byte[] _data = null!;
        internal object[] _meta = null!;

        public NbtToken Root { get; internal set; }

        /// <summary> Parses the specified NBT data to a DOM. </summary>
        /// <param name="copy"> 
        /// Whether to create a copy of the data array.
        /// If false, the parsed DOM will be backed by <paramref name="data"/> directly. 
        /// Note the contents may also be modified to match the platform's native endianess.
        /// </param>
        /// <param name="bytesRead">The size of the NBT document, in bytes.</param>
        public static NbtDocument Parse(byte[] data, int offset, bool copy, out int bytesRead)
        {
            return new NbtParser().Parse(data, offset, copy, out bytesRead);
        }

        internal T GetMeta<T>(int slot) where T : class
        {
            Debug.Assert(_meta[slot].GetType() == typeof(T));
            return Unsafe.As<T>(_meta[slot]);
        }

        public override string ToString()
        {
            return Root.ToString();
        }
    }
}