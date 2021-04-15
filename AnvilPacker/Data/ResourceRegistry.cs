using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Data
{
    /// <summary>
    /// Implements a dictionary that maps <see cref="ResourceName"/> and ID integers to <typeparamref name="T"/> entries.
    /// </summary>
    public class ResourceRegistry<T> : IEnumerable<T>
        where T : class
    {
        private readonly List<T> _idToEntry;
        private readonly Dictionary<T, int> _entryToId;
        private readonly Dictionary<ResourceName, int> _nameToId;

        private bool _frozen = false;

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

        /// <summary> Adds an entry to the registry, if it has not yet been frozen. </summary>
        /// <exception cref="InvalidOperationException"> If the registry was frozen. </exception>
        public void Add(ResourceName name, T val)
        {
            if (_frozen) {
                throw new InvalidOperationException("Cannot modify Registry after freezing it.");
            }
            int id = _idToEntry.Count;
            _idToEntry.Add(val);
            _entryToId.Add(val, id);
            _nameToId.Add(name, id);
        }
        /// <summary> 
        /// Changes the state of the registry to read only. 
        /// <see cref="Add(ResourceName, T)"/> will throw a <see cref="InvalidOperationException"/>
        /// after calling this method
        /// </summary>
        public ResourceRegistry<T> Freeze()
        {
            _frozen = true;
            return this;
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

        public IEnumerator<T> GetEnumerator() => _idToEntry.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
