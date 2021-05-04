﻿using System;
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
    }
}
