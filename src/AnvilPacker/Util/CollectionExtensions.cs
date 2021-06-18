#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    public static class CollectionExtensions
    {
        public static void Fill<T>(this T[] arr, T value)
        {
            arr.AsSpan().Fill(value);
        }
        public static void Clear<T>(this T[] arr)
        {
            arr.AsSpan().Clear();
        }

        public static void Shuffle<T>(this IList<T> list, Func<int, int> rng)
        {
            for (int i = list.Count - 1; i > 0; i--) {
                int j = rng(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static int BinarySearch<T>(this IList<T> list, Func<T, int, int> compare)
        {
            return BinarySearch(list, 0, list.Count, compare);
        }
        public static int BinarySearch<T>(this IList<T> list, int start, int count, Func<T, int, int> compare)
        {
            if (start < 0 || count < 0 || start + count > list.Count) {
                throw new ArgumentOutOfRangeException();
            }
            int end = start + count - 1;
            while (start <= end) {
                int mid = start + (end - start) / 2;
                int c = compare(list[mid], mid);
                if (c < 0) {
                    end = mid - 1;
                } else if (c > 0) {
                    start = mid + 1;
                } else {
                    return mid;
                }
            }
            return ~start;
        }

        public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T?> src) where T : class
        {
            return src.Where(v => v != null)!;
        }

        public static int Count<T>(this T[] arr, T value) where T : IEquatable<T>
        {
            int count = 0;
            foreach (var elem in arr) {
                if (elem.Equals(value)) {
                    count++;
                }
            }
            return count;
        }

        public static int CombinedHashCode<T>(this IEnumerable<T> values)
        {
            var hash = new HashCode();
            foreach (var val in values) {
                hash.Add(val);
            }
            return hash.ToHashCode();
        }

        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, Func<TValue> valueFactory)
            where TKey : notnull
        {
            if (!dict.TryGetValue(key, out var val)) {
                val = valueFactory();
                dict.Add(key, val);
            }
            return val;
        }
    }
}
