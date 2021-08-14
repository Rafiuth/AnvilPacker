using System;
using System.Diagnostics;
using System.Numerics;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    using static Direction;
    using static Axis;

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
    public enum Axis
    {
        X, Y, Z
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
        public static readonly Direction[] All = {
            XNeg, XPos,
            ZNeg, ZPos,
            YNeg, YPos,
        };
        public static readonly Axis[] Axes = {
            X, X,
            Z, Z,
            Y, Y
        };
        public static readonly Direction[] VanillaDirs = { //Index: Vanilla
            YNeg, YPos,
            ZNeg, ZPos,
            XNeg, XPos
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
        /// <remarks> Undefined behavior if <paramref name="dir"/> is a bitmask containing more than one value. </remarks>
        public static int Index(this Direction dir)
        {
            if (dir == Direction.None) {
                return -1;
            }
            int index = BitOperations.TrailingZeroCount((int)dir);
            return VanillaIndices[index];
        }
        /// <summary> Returns the direction from the specified vanilla index, or None if the index is negative. </summary>
        public static Direction FromIndex(int index)
        {
            return index < 0 ? None : VanillaDirs[index];
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

        /// <summary> 
        /// Returns the opposite sides of the specified mask. <br/>
        /// <c>XNeg &lt;-> XPos</c> <br/>
        /// <c>YNeg &lt;-> YPos</c> <br/>
        /// <c>ZNeg &lt;-> ZPos</c> <br/>
        /// </summary>
        public static Direction Opposite(this Direction mask)
        {
            int m = (int)mask;
            return (Direction)(
                (m & 0b101010) >> 1 |
                (m & 0b010101) << 1
            );
        }

        /// <summary> Checks if the direction is any of { XNeg, YNeg, ZNeg } </summary>
        public static bool AnyNeg(this Direction dir)
        {
            return (dir & (XNeg | YNeg | ZNeg)) != 0;
        }
        /// <summary> Checks if the direction is any of { XPos, YPos, ZPos } </summary>
        public static bool AnyPos(this Direction dir)
        {
            return (dir & (XPos | YPos | ZPos)) != 0;
        }

        /// <summary> Returns the coordinate axis for the specified direction </summary>
        public static Axis Axis(this Direction dir)
        {
            int index = BitOperations.TrailingZeroCount((int)dir);
            return Axes[index];
        }
    }
}