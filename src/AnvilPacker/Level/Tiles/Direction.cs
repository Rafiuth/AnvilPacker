using System;
using System.Diagnostics;
using System.Numerics;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    [Flags]
    public enum Direction : byte
    {
        None = 0,
        XNeg = 1 << 0,
        XPos = 1 << 1,
        ZNeg = 1 << 2,
        ZPos = 1 << 3,
        YNeg = 1 << 4,
        YPos = 1 << 5,

        AllHorz = XNeg | XPos | ZNeg | ZPos,
        AllVert = YNeg | YPos,
        All = AllHorz | AllVert,
    }

    public static class Directions
    {
        public static readonly Vec3i[] Normals = { //Index: IR
            new(-1, 0, 0),
            new(+1, 0, 0),
            new(0, 0, -1),
            new(0, 0, +1),
            new(0, -1, 0),
            new(0, +1, 0),
        };
        public static readonly Direction[] VanillaDirs = { //Index: Vanilla
            Direction.YNeg, Direction.YPos,
            Direction.ZNeg, Direction.ZPos,
            Direction.XNeg, Direction.XPos
        };
        public static readonly int[] VanillaIndices = { //Index: IR
            4, 5,
            2, 3,
            0, 1,
        };
        public static readonly string[] VanillaNames = { //Index: Vanilla
            "Down", "Up",
            "North", "South",
            "West", "East"
        };

        /// <summary> Returns the vanilla's direction index from the specified direction, or -1 if <paramref name="dir"/> == None. </summary>
        /// <remarks> Undefined behavior if more than one direction is combined in <paramref name="dir"/>. </remarks>
        public static int Index(this Direction dir)
        {
            if (dir == Direction.None) {
                return -1;
            }
            int index = BitOperations.TrailingZeroCount((int)dir);
            return VanillaIndices[index];
        }
        public static Direction FromIndex(int index)
        {
            return index < 0 ? Direction.None : VanillaDirs[index];
        }

        /// <summary> Converts a vanilla direction mask to our IR mask. </summary>
        public static Direction FromVanillaMask(int mask)
        {
            return (Direction)(
                (mask >> 0 & 0b11) << 4 |
                (mask >> 2 & 0b11) << 2 |
                (mask >> 4 & 0b11) << 0
            );
        }
    }
}