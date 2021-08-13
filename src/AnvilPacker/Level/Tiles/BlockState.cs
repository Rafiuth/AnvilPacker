using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Level.Physics;

namespace AnvilPacker.Level
{
    using BlockPropertyValue = KeyValuePair<string, string>;
    
    public class BlockState : IEquatable<BlockState>
    {
        /// <summary> 
        /// Unique ID for this block state, or -1 if the block is unknown. <br/>
        /// When <see cref="BlockAttributes.HasSidedTransparency"/> is set, this value represents the numeric block ID.
        /// </summary>
        public int Id { get; set; }
        public Block Block { get; set; }
        public BlockPropertyValue[] Properties { get; set; } = Array.Empty<BlockPropertyValue>();
        public BlockAttributes Attributes { get; set; }

        /// <summary> Amount of light this block absorbs. [0..15] </summary>
        public byte LightOpacity { get; set; }
        /// <summary> Amount of light this block emits. [0..15] </summary>
        public byte LightEmission { get; set; }

        public BlockMaterial Material => Block.Material;

        public VoxelShape OcclusionShape { get; set; } = VoxelShape.Cube;

        public bool HasAttrib(BlockAttributes attribs)
        {
            return (Attributes & attribs) == attribs;
        }

        public bool Equals(BlockState other)
        {
            return (Id >= 0 ? Id == other.Id : SlowEquals(other)) &&
                   (Attributes & BlockAttributes.InternalMask) == (other.Attributes & BlockAttributes.InternalMask);
        }
        private bool SlowEquals(BlockState other)
        {
            return Block.Name == other.Block.Name &&
                   Properties.SequenceEqual(other.Properties);
        }
        public override bool Equals(object obj)
        {
            return obj is BlockState mb && Equals(mb);
        }
        public override int GetHashCode()
        {
            return Id >= 0 ? Id : Block.Name.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Block.Name.ToString(appendDefaultNamespace: false));

            if (Properties.Length > 0) {
                sb.Append('[');
                int i = 0;
                foreach (var (k, v) in Properties) {
                    sb.Append(i++ == 0 ? "" : ",");
                    sb.Append(k);
                    sb.Append('=');
                    sb.Append(v);
                }
                sb.Append(']');
            }
            return sb.ToString();
        }
    }
}
