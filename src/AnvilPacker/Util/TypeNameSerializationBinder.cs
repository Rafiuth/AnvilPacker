using System;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;

namespace AnvilPacker.Util
{
    //https://stackoverflow.com/a/12203624
    public class TypeNameSerializationBinder : DefaultSerializationBinder
    {
        private readonly Dictionary<Type, string> _typeToName = new();
        private readonly Dictionary<string, Type> _nameToType = new(StringComparer.OrdinalIgnoreCase);

        public TypeNameSerializationBinder Map(Type type, string name)
        {
            _typeToName.Add(type, name);
            _nameToType.Add(name, type);
            return this;
        }
        public TypeNameSerializationBinder Map(IEnumerable<KeyValuePair<Type, string>> types)
        {
            foreach (var (type, name) in types) {
                Map(type, name);
            }
            return this;
        }
        public TypeNameSerializationBinder Map(IEnumerable<KeyValuePair<string, Type>> types)
        {
            foreach (var (name, type) in types) {
                Map(type, name);
            }
            return this;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = _typeToName.GetValueOrDefault(serializedType);
        }
        public override Type BindToType(string assemblyName, string typeName)
        {
            return _nameToType.GetValueOrDefault(typeName);
        }
    }
}