using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class Block : IEquatable<Block>
    {
        /// <summary> 
        /// Unique ID for this block, or -1 if this block is unknown (<see cref="IsKnown"/> is false). <br/>
        /// This value never represents legacy numeric block IDs.
        /// </summary>
        public int Id { get; init; }
        public ResourceName Name { get; init; }
        public BlockState DefaultState { get; set; }
        public BlockMaterial Material { get; init; }
        public List<BlockProperty> Properties { get; init; }
        public BlockState[] States { get; init; }

        /// <summary> Whether this block is known and has valid attributes. </summary>
        public bool IsKnown => Id >= 0;

        /// <summary> Creates a copy of this block with the new name. </summary>
        public Block Rename(ResourceName newName)
        {
            Ensure.That(IsKnown, "Only known blocks can be renamed");
            
            var states = new BlockState[States.Length];
            var newBlock = new Block() {
                Id = BlockRegistry.NextBlockId(),
                Name = newName,
                Material = Material,
                Properties = Properties,
                States = states
            };

            for (int i = 0; i < states.Length; i++) {
                var oldState = States[i];

                states[i] = new BlockState() {
                    Id = BlockRegistry.NextStateId(),
                    Block = newBlock,
                    Properties = oldState.Properties,
                    Attributes = oldState.Attributes,
                    LightOpacity = oldState.LightOpacity,
                    LightEmission = oldState.LightEmission,
                    OcclusionShape = oldState.OcclusionShape
                };
            }
            newBlock.DefaultState = states[DefaultState.Id - States[0].Id];
            return newBlock;
        }

        public bool Equals(Block other)
        {
            if (IsKnown && other.IsKnown) {
                return other.Id == Id;
            }
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
        Opaque              = 1 << 0, //fabric: isOpaque(), mojang: canOcclude()
        Translucent         = 1 << 1, //fabric: isTranslucent(), mojang: propagatesSkylightDown()
        FullCube            = 1 << 2, //fabric: isFullCube(), mojang: isCollisionShapeFullBlock()
        HasSidedTransparency= 1 << 3,
        HasRandomTicks      = 1 << 4,
        EmitsRedstonePower  = 1 << 5, //fabric: emitsRedstonePower(), mojang: isSignalSource()
        HasFluid            = 1 << 6, // !state.getFluidState().isEmpty()
        HasDynamicShape     = 1 << 7,
        
        //Internal
        Legacy              = 1 << 28,
        InternalMask        = ~0 << 28
    }
}