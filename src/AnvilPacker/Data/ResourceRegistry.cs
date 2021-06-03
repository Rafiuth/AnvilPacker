using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Implements a dictionary that maps <see cref="ResourceName"/> and ID integers to <typeparamref name="T"/> entries.
    /// </summary>
    public class ResourceRegistry<T> : IEnumerable<T>
        where T : class
    {
        private readonly DictionarySlim<int, T> _idToEntry;
        private readonly Dictionary<T, int> _entryToId;
        private readonly Dictionary<ResourceName, int> _nameToId;

        public int EntryCount => _idToEntry.Count;

        public T this[int id] => Get(id);
        public T this[ResourceName name] => Get(name);
        public int this[T obj] => GetId(obj);

        public ResourceRegistry(int capacity = 256)
        {
            _idToEntry = new(capacity);
            _entryToId = new(capacity, ReferenceEqualityComparer.Instance);
            _nameToId = new(capacity);
        }

        public void Add(ResourceName name, T val)
        {
            Add(name, val, _idToEntry.Count);
        }
        public void Add(ResourceName name, T val, int id)
        {
            _idToEntry.Add(id, val);
            _entryToId.Add(val, id);
            _nameToId.Add(name, id);
        }

        public T Get(int id) => _idToEntry[id];
        public T Get(ResourceName name) => Get(_nameToId[name]);
        public int GetId(T obj) => _entryToId[obj];

        public IEnumerator<(int Id, ResourceName Name, T Value)> GetEntries()
        {
            foreach (var kv in _nameToId) {
                yield return (kv.Value, kv.Key, _idToEntry[kv.Value]);
            }
        }

        public IEnumerator<T> GetEnumerator() => _entryToId.Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
