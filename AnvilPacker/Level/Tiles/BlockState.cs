using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    // Design note: The reason we need the block registry is because block states can be
    // ambiguous by omitting properties.
    // Consider:
    //   Both `oak_log[axis=y]` and `oak_log` map to the same block state. If we didn't knew 
    //   that the default value for `axis` is `y`, they would map to entirely different block states.
    // The unfortunate consequences of this are:
    //  - Slow startup since we have to parse a huge JSON file and create thousands of objects.
    //  - Lost support for modded worlds. (still possible, but requires the hastle of updating `blocks.json`)
    //  - Since 1.13, blocks can be renamed. A complete registry is needed for every version being used.
    public class BlockState : IEquatable<BlockState>
    {
        public static BlockState Air { get; internal set; }

        public int Id { get; init; }
        public Block Block { get; init; }
        public Dictionary<string, BlockPropertyValue> Properties { get; init; }
        public BlockAttributes Attributes { get; init; }

        /// <summary> Amount of light this block absorbs. [0..15] </summary>
        public byte Opacity { get; init; }
        /// <summary> Amount of light this block emits. [0..15] </summary>
        public byte Emittance { get; init; }

        public BlockMaterial Material => Block.Material;

        public BlockPropertyValue GetProperty(string name)
        {
            return Properties[name];
        }
        public BlockState WithProperty(string name, string value)
        {
            var prop = GetProperty(name);
            return WithProperty(prop, prop.GetIndex(value));
        }
        private BlockState WithProperty(BlockPropertyValue prop, int valueIndex)
        {
            int newId = ChangeDigit(
                x: Id - Block.MinStateId,
                newVal: valueIndex,
                shift: prop.IdShift,
                radix: prop.ValueCount
            );
            return Block.StateRegistry[Block.MinStateId + newId];

            static int ChangeDigit(int x, int newVal, int shift, int radix)
            {
                int currVal = (x / shift) % radix;
                return x + (newVal - currVal) * shift;
            }
        }

        public bool Equals(BlockState other)   => other.Id == Id;
        public override bool Equals(object obj) => obj is BlockState mb && Equals(mb);
        public override int GetHashCode()       => Id;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Block.Name.ToString(appendDefaultNamespace: false));

            if (Properties.Count > 0) {
                sb.Append('[');
                int i = 0;
                foreach (var (k, v) in Properties) {
                    sb.Append(i++ == 0 ? "" : ",");
                    sb.Append(k);
                    sb.Append('=');
                    sb.Append(v.GetValue());
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        /// <summary> Parses a block state in the form of <c>namespace:block_name[property1=value1,property2=value2,...]</c> </summary>
        public static BlockState Parse(string str)
        {
            int i = str.IndexOf('[');
            if (i < 0) i = str.Length;

            var blockName = ResourceName.Parse(str[0..i]);
            var block = Block.Registry[blockName].DefaultState;

            i++; //skip '['
            for (; i < str.Length; i++) {
                int iEq = str.IndexOf('=', i);
                int iValEnd = str.IndexOf(',', iEq);
                if (iValEnd < 0) iValEnd = str.Length - 1;

                var propName = str[i..iEq];
                var propVal = str[(iEq + 1)..iValEnd];

                block = block.WithProperty(propName, propVal);

                // expect ',' or ']' if end
                bool isEnd = iValEnd == str.Length - 1;
                if (str[iValEnd] != (isEnd ? ']' : ',')) {
                    throw new FormatException($"Malformed block state string '{str}': expecting ',' or ']', got '{str[iValEnd]}'");
                }
                i = iValEnd;
            }
            return block;
        }
    }
}
