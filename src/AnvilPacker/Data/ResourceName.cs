using System;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public struct ResourceName : IEquatable<ResourceName>, IComparable<ResourceName>
    {
        public const string DefaultNamespace = "minecraft";

        public string Namespace { get; }
        public string Path { get; }

        /// <summary> Creates a <see cref="ResourceName"/> with the default namespace and the specified path. </summary>
        public ResourceName(string path)
        {
            Namespace = DefaultNamespace;
            Path = path;
        }
        public ResourceName(string ns, string path)
        {
            Namespace = ns;
            Path = path;
        }

        public static ResourceName Parse(string str)
        {
            int colon = str.IndexOf(':');
            //empty namespace or path = ":abc" or "abc:"
            if (str.Length == 0 || colon == 0 || colon == str.Length - 1) {
                throw new FormatException($"Empty namespace or path in resource name. '{str}'");
            }
            
            if (colon < 0) {
                return new ResourceName(DefaultNamespace, str);
            } else {
                return new ResourceName(str[0..colon], str[(colon + 1)..]);
            }
        }

        public int CompareTo(ResourceName other)
        {
            int c = Path.CompareTo(other.Path);
            if (c == 0) c = Namespace.CompareTo(other.Namespace);

            return c;
        }

        public override string ToString() => ToString();

        public string ToString(bool appendDefaultNamespace = true)
        {
            if (Namespace == DefaultNamespace && !appendDefaultNamespace) {
                return Path;
            }
            return $"{Namespace}:{Path}";
        }

        public bool Equals(ResourceName other) => Namespace == other.Namespace && Path == other.Path;
        public override bool Equals(object obj) => obj is ResourceName other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Namespace, Path);

        public static bool operator ==(ResourceName left, ResourceName right) => left.Equals(right);
        public static bool operator !=(ResourceName left, ResourceName right) => !left.Equals(right);

        public static implicit operator ResourceName(string path) => Parse(path);
    }
}
