using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    public class Verifier
    {
        public static string HashBlocks(RegionBuffer region)
        {
            return HashChunks(region, 0, (hasher, section) => {
                hasher.AppendData(MemoryMarshal.AsBytes(section.Blocks.AsSpan()));
            });
        }

        public static string HashLight(RegionBuffer region)
        {
            return HashChunks(region, 1, (hasher, section) => {
                if (section.BlockLight != null) {
                    hasher.AppendData(section.BlockLight.Data);
                }
                if (section.SkyLight != null) {
                    hasher.AppendData(section.SkyLight.Data);
                }
            });
        }

        public static bool CompareBlocks(RegionBuffer r1, RegionBuffer r2)
        {
            if (!ComparePalettes(r1.Palette, r2.Palette)) {
                return false;
            }
            return CompareChunks(r1, r2, (s1, s2) => {
                return s1.Blocks.AsSpan().SequenceEqual(s2.Blocks); 
            });
        }

        private static bool ComparePalettes(BlockPalette palette1, BlockPalette palette2)
        {
            //don't care if one is larger, IDs just need to refer to the same block
            int len = Math.Min(palette1.Count, palette2.Count);
            for (int i = 0; i < len; i++) {
                var b1 = palette1.GetState((BlockId)i);
                var b2 = palette2.GetState((BlockId)i);
                if (!b1.Equals(b2)) {
                    return false;
                }
            }
            return true;
        }

        public static bool CompareLight(RegionBuffer r1, RegionBuffer r2)
        {
            return CompareChunks(r1, r2, (s1, s2) => {
                return CompareData(s1.BlockLight, s2.BlockLight) &&
                       CompareData(s1.SkyLight, s2.SkyLight);
            });

            static bool CompareData(NibbleArray n1, NibbleArray n2)
            {
                if (n1 == null || n2 == null) {
                    return n1 == n2;
                }
                return n1.Data.AsSpan().SequenceEqual(n2.Data);
            }
        }

        public static bool CompareMetadata(RegionBuffer r1, RegionBuffer r2)
        {
            if (r1.Size != r2.Size) {
                return false;
            }
            for (int i = 0; i < r1.Chunks.Length; i++) {
                var c1 = r1.Chunks[i];
                var c2 = r2.Chunks[i];
                if ((c1 == null) != (c2 == null)) {
                    return false;
                }
                if (c1 == null) continue;
                
                if (c1.DataVersion != c2.DataVersion) {
                    return false;
                }
                if (!CompareTags(c1.Opaque, c2.Opaque)) {
                    return false;
                }
            }
            return true;
        }

        private static bool CompareChunks(RegionBuffer r1, RegionBuffer r2, Func<ChunkSection, ChunkSection, bool> pred)
        {
            if (r1.Size != r2.Size) {
                return false;
            }
            var itr1 = ChunkIterator.GetSections(r1);
            var itr2 = ChunkIterator.GetSections(r2);
            foreach (var (s1, s2) in itr1.Zip(itr2)) {
                if ((s1 == null) != (s2 == null)) {
                    return false; //r1.NumSections != r2.NumSections
                }
                if (s1.X != s2.X || s1.Y != s2.Y || s1.Z != s2.Z) {
                    return false;
                }
                if (!pred(s1, s2)) {
                    return false;
                }
            }
            return true;
        }
        private static string HashChunks(RegionBuffer region, int typeTag, Action<IncrementalHash, ChunkSection> appendData)
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var header = new byte[16];
            Mem.WriteLE(header, 0, 0xABCD0000 + typeTag);

            foreach (var section in ChunkIterator.GetSections(region)) {
                Mem.WriteLE(header, 4, section.X);
                Mem.WriteLE(header, 8, section.Y);
                Mem.WriteLE(header, 12, section.Z);
                hasher.AppendData(header);
                appendData(hasher, section);
            }
            var hash = hasher.GetCurrentHash();
            return BitConverter.ToString(hash).Replace("-", "");
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