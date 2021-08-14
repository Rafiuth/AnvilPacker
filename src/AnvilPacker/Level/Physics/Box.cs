using System;
using System.Runtime.CompilerServices;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Physics
{
    /// <summary> An 8-bit integer 3D AABB. </summary>
    public readonly struct Box8 : IEquatable<Box8>
    {
        public readonly sbyte 
            MinX, MinY, MinZ,
            MaxX, MaxY, MaxZ;

#pragma warning disable CS0414 //unused field
        private readonly ushort _pad;

        private ulong _bits => Unsafe.As<Box8, ulong>(ref Unsafe.AsRef(in this));

        /// <summary> Gets/sets the coord at the specified index, in the order of [MinX, MinY, MinZ, MaxX, MaxY, MaxZ] </summary>
        public sbyte this[int index]
        {
            get {
                Ensure.That((uint)index < 6);
                return Unsafe.Add(ref Unsafe.AsRef(in MinX), index);
            }
        }

        public sbyte Min(Axis axis) => this[(int)axis + 0];
        public sbyte Max(Axis axis) => this[(int)axis + 3];

        public Box8(
            sbyte minX, sbyte minY, sbyte minZ, 
            sbyte maxX, sbyte maxY, sbyte maxZ
        )
        {
            (MinX, MinY, MinZ) = (minX, minY, minZ);
            (MaxX, MaxY, MaxZ) = (maxX, maxY, maxZ);
            _pad = 0;
        }
        public Box8(ReadOnlySpan<sbyte> coords)
        {
            (MinX, MinY, MinZ) = (coords[0], coords[1], coords[2]);
            (MaxX, MaxY, MaxZ) = (coords[3], coords[4], coords[5]);
            _pad = 0;
        }

        public bool Equals(Box8 other)
        {
            return _bits == other._bits;
        }
        public override bool Equals(object obj)
        {
            return obj is Box8 other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(_bits);
        }

        public override string ToString()
        {
            return $"Box({MinX} {MinY} {MinZ}, {MaxX} {MaxY} {MaxZ})";
        }
        public static bool operator ==(in Box8 left, in Box8 right) => left.Equals(right);
        public static bool operator !=(in Box8 left, in Box8 right) => !(left == right);
    }
}