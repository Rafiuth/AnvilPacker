using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public class Block
    {
        public static ResourceRegistry<Block> Registry { get; internal set; }
        public static IndexedMap<BlockState> StateRegistry { get; internal set; }

        public ResourceName Name { get; init; }
        public int MinStateId { get; init; }
        public int MaxStateId { get; init; }
        public BlockState DefaultState { get; internal set; }
        public IReadOnlyList<BlockProperty> Properties { get; init; }

        public BlockMaterial Material { get; init; }

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
        OpaqueFullCube      = 1 << 3, // Opaque && FullCube
        HasRandomTicks      = 1 << 4,
        EmitsRedstonePower  = 1 << 5,
        IsImmerse           = 1 << 6, // !state.getFluidState().isEmpty()
        IsAir               = 1 << 7
    }
}