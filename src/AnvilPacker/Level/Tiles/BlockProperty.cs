using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level
{
    public abstract class BlockProperty : IEquatable<BlockProperty>
    {
        public string Name { get; init; }
        public string[] Values { get; init; }
        public int NumValues => Values.Length;

        public virtual string GetValue(int index)
        {
            return Values[index];
        }
        public int GetIndex(string str)
        {
            if (TryGetIndex(str, out int index)) {
                return index;
            }
            throw new FormatException($"Value '{str}' does not exist in property '{Name}' of type '{GetType().Name}'");
        }
        public virtual bool TryGetIndex(string str, out int index)
        {
            index = Array.IndexOf(Values, str);
            return index >= 0;
        }

        public abstract bool Equals(BlockProperty other);
        public override abstract int GetHashCode();
    }

    public class BoolProperty : BlockProperty
    {
        public BoolProperty(string name)
        {
            Name = name;
            Values = new[] { "true", "false" }; //inverse binary, mojang logik
        }

        public override bool Equals(BlockProperty other)
        {
            return other is BoolProperty p && p.Name == Name;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name);
        }
    }
    public class IntProperty : BlockProperty
    {
        public int Min { get; }
        public int Max { get; }

        public IntProperty(string name, int min, int max)
        {
            Name = name;
            Min = min;
            Max = max;
            Values = new string[max - min + 1];
            for (int i = min; i <= max; i++) {
                Values[i - min] = i.ToString();
            }
        }
        public override bool TryGetIndex(string str, out int index)
        {
            if (int.TryParse(str, out index) && index >= Min && index <= Max) {
                index -= Min;
                return true;
            }
            return false;
        }

        public override bool Equals(BlockProperty other)
        {
            return other is IntProperty p &&
                   p.Name == Name &&
                   p.Min == Min &&
                   p.Max == Max;
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Min, Max);
        }
    }
    public class EnumProperty : BlockProperty
    {
        public EnumProperty(string name, string[] values)
        {
            Name = name;
            Values = values;
        }

        public override bool Equals(BlockProperty other)
        {
            return other is EnumProperty p &&
                   p.Name == Name &&
                   p.Values.SequenceEqual(Values);
        }
        public override int GetHashCode()
        {
            //omitting Values here is fine, this just increases collision prob
            return HashCode.Combine(Name);
        }
    }
}
