﻿using System;
using System.Collections.Generic;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public class Block
    {
        public ResourceName Name { get; init; }
        public BlockState DefaultState { get; set; }
        public BlockMaterial Material { get; init; }
        public List<BlockProperty> Properties { get; init; }
        public BlockState[] States { get; set; }

        /// <summary> Whether this block was created on the fly, i.e. it is not known in any registry. </summary>
        public bool IsDynamic { get; init; }

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
        IsAir               = 1 << 7,
        //TODO: Remove IsAir and OpaqueFullCube

        //Internal
        Legacy              = 1 << 30,
        InternalMask        = Legacy
    }
}