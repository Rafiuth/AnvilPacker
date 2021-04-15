using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    // Design note: The reason we need the block registry is because we wouldn't know how to index them otherwise.
    // Consider the following:
    //   Both `oak_log[axis=y]` and `oak_log` map to the same block state. We must know blocks 
    //   and their default states, otherwise they would map to entirely different block states.
    // The unfortunate consequences of this are:
    //  - Slow startup since we have to parse a huge JSON file and create thousands of objects.
    //  - Lost support for modded worlds. (still possible, but requires the hastle of updating `blocks.json`)
    //  - Requires a complete registry of every version; Mojang may add or rename a block, and I'd like to keep compatibility with all versions.
    public class MBlockState : IBlockState, IEquatable<MBlockState>
    {
        public static MBlockState Air { get; internal set; }

        public int Id { get; init; }
        public MBlock Block { get; init; }
        public Dictionary<string, BlockPropertyValue> Properties { get; init; }
        public BlockAttributes Attributes { get; init; }

        public BlockMaterial Material => Block.Material;

        public BlockPropertyValue GetProperty(string name)
        {
            return Properties[name];
        }
        public MBlockState WithProperty(string name, string value)
        {
            var prop = GetProperty(name);
            return WithProperty(prop, prop.GetIndex(value));
        }
        private MBlockState WithProperty(BlockPropertyValue prop, int valueIndex)
        {
            int newId = ChangeDigit(
                x: Id - Block.MinStateId,
                newVal: valueIndex,
                shift: prop.IdShift,
                radix: prop.ValueCount
            );
            return MBlock.StateRegistry[Block.MinStateId + newId];

            static int ChangeDigit(int x, int newVal, int shift, int radix)
            {
                int currVal = (x / shift) % radix;
                return x + (newVal - currVal) * shift;
            }
        }

        public bool Equals(MBlockState other)   => other.Id == Id;
        public override bool Equals(object obj) => obj is MBlockState mb && Equals(mb);
        public override int GetHashCode()       => Id;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Block.Name.ToString(appendDefaultNamespace: false));

            if (Properties.Count > 0) {
                sb.Append('[');
                foreach (var (k, v) in Properties) {
                    sb.Append(k);
                    sb.Append('=');
                    sb.Append(v.GetValue());
                }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }

    [Flags]
    public enum BlockAttributes
    {
        None                = 0,
        Opaque              = 1 << 0,
        Translucent         = 1 << 1,
        FullCube            = 1 << 2,
        OpaqueFullCube      = 1 << 3,
        HasRandomTicks      = 1 << 4,
        EmitsRedstonePower  = 1 << 5,
        IsAir               = 1 << 7
    }
}
