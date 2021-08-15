using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        /// <summary> Returns a span contanining [MinX, MinY, MinZ, MaxX, MaxY, MaxZ]. </summary>
        /// <remarks> The returned span is backed directly by this struct's reference. Never return it from a method if this struct is a local variable.  </remarks>
        public Span<byte> UnsafeDataSpan
            => MemoryMarshal.CreateSpan(ref Unsafe.As<sbyte, byte>(ref Unsafe.AsRef(in MinX)), 6);

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

        /// <remarks> Note: All parameters are truncated to sbyte. </remarks>
        public Box8(
            int minX, int minY, int minZ,
            int maxX, int maxY, int maxZ
        )
        {
            (MinX, MinY, MinZ) = ((sbyte)minX, (sbyte)minY, (sbyte)minZ);
            (MaxX, MaxY, MaxZ) = ((sbyte)maxX, (sbyte)maxY, (sbyte)maxZ);
            Unsafe.SkipInit(out _pad);
        }
        public Box8(ReadOnlySpan<sbyte> coords)
        {
            (MinX, MinY, MinZ) = (coords[0], coords[1], coords[2]);
            (MaxX, MaxY, MaxZ) = (coords[3], coords[4], coords[5]);
            Unsafe.SkipInit(out _pad);
        }

        public bool Equals(Box8 other)
        {
            return _bits == other._bits;
        }
        public override bool Equals(object? obj)
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