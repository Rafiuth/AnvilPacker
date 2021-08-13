using System;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Physics
{
    public class VoxelShape : IEquatable<VoxelShape>
    {
        public static VoxelShape Empty { get; } = new(new Box8[0]);
        public static VoxelShape Cube { get; } = new(new Box8[] { new(0, 0, 0, 16, 16, 16) });

        public Box8[] Boxes { get; }

        /// <summary> Creates a new voxel shape backed by the specified array. (not copied) </summary>
        public VoxelShape(Box8[] boxes)
        {
            Boxes = boxes;
        }

        public bool Equals(VoxelShape other)
        {
            return Boxes.AsSpan().SequenceEqual(other.Boxes);
        }
        public override bool Equals(object obj)
        {
            return obj is VoxelShape other && Equals(other);
        }
        public override int GetHashCode()
        {
            return Boxes.CombinedHashCode();
        }

        public override string ToString()
        {
            return $"VoxelShape({string.Join(", ", Boxes)})";
        }
    }
}