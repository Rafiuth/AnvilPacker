using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AnvilPacker.Data;
using AnvilPacker.Level;

namespace AnvilPacker
{
    public class Verifier
    {
        public static unsafe string HashBlocks(RegionBuffer region)
        {
            using var hash = SHA256.Create();
            var buf = new byte[4096 * sizeof(BlockId)];
            foreach (var section in ChunkIterator.GetSections(region)) {
                MemoryMarshal.AsBytes(section.Blocks.AsSpan()).CopyTo(buf);
                hash.TransformBlock(buf, 0, buf.Length, null, 0);
            }
            hash.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "");
        }
        public static bool CompareBlocks(RegionBuffer r1, RegionBuffer r2)
        {
            if (r1.Size != r2.Size) {
                return false;
            }
            for (int z = 0; z < r1.Size; z++) {
                for (int x = 0; x < r1.Size; x++) {
                    var c1 = r1.GetChunk(x, z);
                    var c2 = r2.GetChunk(x, z);

                    if ((c1 == null) != (c2 == null)) {
                        return false;
                    }
                    if (c1 != null && !CompareBlocks(c1, c2)) {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool CompareBlocks(Chunk c1, Chunk c2)
        {
            if (c1.MinSectionY != c2.MinSectionY || c1.MaxSectionY != c2.MaxSectionY) {
                return false;
            }
            for (int sy = c1.MinSectionY; sy <= c1.MaxSectionY; sy++) {
                var s1 = c1.GetSection(sy);
                var s2 = c2.GetSection(sy);
                if ((s1 == null) != (s2 == null)) {
                    return false;
                }
                if (s1 != null && !s1.Blocks.AsSpan().SequenceEqual(s2.Blocks)) {
                    Console.WriteLine("Wrong at " + c1.X + " " +sy+" " + c1.Z);
                    return false;
                }
            }
            return true;
        }

        public static bool CompareTags(NbtTag t1, NbtTag t2)
        {
            bool ComparePrim<T>() where T : IEquatable<T>
            {
                var v1 = ((PrimitiveTag<T>)t1).Value;
                var v2 = ((PrimitiveTag<T>)t2).Value;
                return v1.Equals(v2);
            }
            bool CompareArr<T>() where T:IEquatable<T>
            {
                var a1 = ((PrimitiveTag<T[]>)t1).Value;
                var a2 = ((PrimitiveTag<T[]>)t2).Value;
                return a1.AsSpan().SequenceEqual(a2);
            }
            
            if (t1.Type != t2.Type) {
                return false;
            }
            switch (t1.Type) {
                case TagType.Byte:      return ComparePrim<byte>();
                case TagType.Short:     return ComparePrim<short>();
                case TagType.Int:       return ComparePrim<int>();
                case TagType.Long:      return ComparePrim<long>();
                case TagType.Float:     return ComparePrim<float>();
                case TagType.Double:    return ComparePrim<double>();
                case TagType.String:    return ComparePrim<string>();
                case TagType.ByteArray: return CompareArr<byte>();
                case TagType.IntArray:  return CompareArr<int>();
                case TagType.LongArray: return CompareArr<long>();
                case TagType.List: {
                    var list1 = (ListTag)t1;
                    var list2 = (ListTag)t2;
                    if (list1.Count != list2.Count) {
                        return false;
                    }
                    for (int i = 0; i < list1.Count; i++) {
                        if (!CompareTags(list1[i], list2[i])) {
                            return false;
                        }
                    }
                    return true;
                }
                case TagType.Compound: {
                    var c1 = (CompoundTag)t1;
                    var c2 = (CompoundTag)t2; 
                    if (c1.Count != c2.Count) {
                        return false;
                    }
                    foreach (var (name, v1) in c1) {
                        if (!c2.TryGet(name, out NbtTag v2) || !CompareTags(v1, v2)) {
                            return false;
                        }
                    }
                    return true;
                }
                default: throw new NotImplementedException();
            }
        }
    }
}