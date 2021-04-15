using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Data
{
    /// <summary> Implements a readonly map that can be indexed. </summary>
    public class IndexedMap<T> : IEnumerable<T>
    {
        private readonly T[] _arr;
        private readonly Dictionary<T, int> _indices;

        public IndexedMap(T[] entries)
        {
            _arr = entries;
            _indices = new Dictionary<T, int>(_arr.Length);
            for (int i = 0; i < _arr.Length; i++) {
                _indices.Add(_arr[i], i);
            }
        }
        public int Length => _arr.Length;

        public T this[int index] => _arr[index];
        public int this[T elem] => _indices[elem];

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_arr).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
