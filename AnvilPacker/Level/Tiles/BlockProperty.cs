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
        public int ValueCount => Values.Length;

        public virtual string GetValue(int index)
        {
            return Values[index];
        }
        public virtual int GetIndex(string str)
        {
            int index = Array.IndexOf(Values, str);
            if (index >= 0) {
                return index;
            }
            throw new FormatException($"Value '{str}' does not exist in property '{Name}' type '{GetType().Name}'");
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
        public override int GetIndex(string str)
        {
            if (int.TryParse(str, out int value) && value >= Min && value <= Max) {
                return value - Min;
            }
            return -1;
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

    public struct BlockPropertyValue
    {
        public BlockProperty Property { get; init; }
        /// <summary> Index of the value in the property. </summary>
        public int Index { get; init; }

        /// <summary> Divisor used to get the value index of a block state id. </summary>
        public int IdShift { get; init; }

        public int ValueCount => Property.ValueCount;

        /// <param name="props">All properties defined in the block.</param>
        /// <param name="prop">The property for which the value is being created.</param>
        /// <param name="stateId">Block state ID offseted by -<see cref="Block.MinStateId"/></param>
        public static BlockPropertyValue Create(IReadOnlyList<BlockProperty> props, BlockProperty prop, int stateId)
        {
            int shift = 1;

            for (int i = props.Count - 1; i >= 0; i--) {
                var p = props[i];
                if (p == prop) {
                    return new BlockPropertyValue() {
                        Property = prop,
                        Index = (stateId / shift) % prop.ValueCount,
                        IdShift = shift
                    };
                }
                shift *= p.ValueCount;
            }
            throw new InvalidOperationException("Block does not have the specified property.");
        }
        public string GetValue() => Property.GetValue(Index);
        public int GetIndex(string value) => Property.GetIndex(value);
    }
}
