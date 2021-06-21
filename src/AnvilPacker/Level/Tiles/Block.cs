using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public class Block : IEquatable<Block>
    {
        public ResourceName Name { get; init; }
        public BlockState DefaultState { get; set; }
        public BlockMaterial Material { get; init; }
        public List<BlockProperty> Properties { get; init; }
        public BlockState[] States { get; init; }

        /// <summary> Whether this block was created on the fly, i.e. it is not known in any registry. </summary>
        public bool IsDynamic { get; init; }

        public bool Equals(Block other)
        {
            return other.Name == Name && 
                   Properties.SequenceEqual(other.Properties);
        }
        public override bool Equals(object obj)
        {
            return obj is Block b && Equals(b);
        }
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
    
    [Flags]
    public enum BlockAttributes
    {
        None                = 0,
        Opaque              = 1 << 0,
        Translucent         = 1 << 1,
        FullCube            = 1 << 2,
        //OpaqueFullCube      = 1 << 3, // Opaque && FullCube
        HasRandomTicks      = 1 << 4,
        EmitsRedstonePower  = 1 << 5,
        HasFluid            = 1 << 6, // !state.getFluidState().isEmpty()
        //IsAir               = 1 << 7,
        //TODO: Remove IsAir and OpaqueFullCube from datagen

        //Internal
        Legacy              = 1 << 28,
        InternalMask        = ~0 << 28
    }
}