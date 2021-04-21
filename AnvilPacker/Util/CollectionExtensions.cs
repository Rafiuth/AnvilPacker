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
    }
}
