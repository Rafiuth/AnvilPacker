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
        /// <summary> Fills the array with the specified value. </summary>
        public static void Fill<T>(this T[] arr, T value)
        {
            arr.AsSpan().Fill(value);
        }
        /// <summary> Fills the array with the default value of <typeparamref name="T"/>. </summary>
        public static void Clear<T>(this T[] arr)
        {
            arr.AsSpan().Clear();
        }

        /// <summary> Shuffles the list elements using a custom RNG. </summary>
        /// <param name="rng">A delegate in the form `int rng(int max)` which generates random numbers in the range [0..max) </param>
        public static void Shuffle<T>(this List<T> list, Func<int, int> rng)
        {
            for (int i = list.Count - 1; i > 0; i--) {
                int j = rng(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        /// <summary> Performs a binary search over the specified list, using a custom comparer. </summary>
        /// <param name="compare">A delegate in the form `int compare(T elem, int elemIndex)` which the difference from the value being searched. (less than 0, 0 for equal, greater than 0). </param>
        /// <returns>The index of the element, or, if the element was not found, a negative value, which is the bitwise complement value indicating where the first value would be.</returns>
        public static int BinarySearch<T>(this List<T> list, Func<T, int, int> compare)
        {
            return BinarySearch(list, 0, list.Count, compare);
        }
        public static int BinarySearch<T>(this List<T> list, int start, int count, Func<T, int, int> compare)
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

        /// <summary> Filters <c>null</c> values from the specified Enumerable. </summary>
        public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T?> src) where T : class
        {
            return src.Where(v => v != null)!;
        }

        /// <summary> Returns the number of values equal to the specified value. </summary>
        public static int Count<T>(this T[] arr, T value) where T : IEquatable<T>
        {
            return Count(arr.AsSpan(), value);
        }
        /// <summary> Returns the number of values equal to the specified value. </summary>
        public static int Count<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            int count = 0;
            foreach (var elem in span) {
                if (elem.Equals(value)) {
                    count++;
                }
            }
            return count;
        }

        /// <summary> Returns whether all elements in the array are equal the specified value. </summary>
        public static bool All<T>(this T[] arr,  T value) where T : IEquatable<T>
        {
            return Mem.AllEquals(arr, value);
        }
        /// <summary> Returns whether all elements in the array are equal the specified value. </summary>
        public static bool All<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
        {
            return Mem.AllEquals(span, value);
        }

        /// <summary> Computes a hash code of all elements in the specified Enumerable. </summary>
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
