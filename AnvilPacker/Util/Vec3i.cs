using System;

namespace AnvilPacker.Util
{
    public struct Vec3i : IEquatable<Vec3i>
    {
        public int X, Y, Z;

        public int Volume => X * Y * Z;

        public Vec3i(int x, int y, int z) 
            => (X, Y, Z) = (x, y, z);

        public Vec3i(int v) 
            => (X, Y, Z) = (v, v, v);

        public bool Equals(Vec3i other) => other.X == X && other.Y == Y && other.Z == Z;
        public override bool Equals(object obj) => obj is Vec3i other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"Vec3i({X}, {Y}, {Z})";

        public static bool operator ==(Vec3i left, Vec3i right) => left.Equals(right);
        public static bool operator !=(Vec3i left, Vec3i right) => !left.Equals(right);

        public static Vec3i operator +(Vec3i a, Vec3i b) => new Vec3i(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3i operator -(Vec3i a, Vec3i b) => new Vec3i(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3i operator *(Vec3i a, Vec3i b) => new Vec3i(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        public static Vec3i operator /(Vec3i a, Vec3i b) => new Vec3i(a.X / b.X, a.Y / b.Y, a.Z / b.Z);

        public static Vec3i operator +(Vec3i a, int b) => new Vec3i(a.X + b, a.Y + b, a.Z + b);
        public static Vec3i operator -(Vec3i a, int b) => new Vec3i(a.X - b, a.Y - b, a.Z - b);
        public static Vec3i operator *(Vec3i a, int b) => new Vec3i(a.X * b, a.Y * b, a.Z * b);
        public static Vec3i operator /(Vec3i a, int b) => new Vec3i(a.X / b, a.Y / b, a.Z / b);
        public static Vec3i operator %(Vec3i a, int b) => new Vec3i(a.X % b, a.Y % b, a.Z % b);

        public static Vec3i operator &(Vec3i a, int b) => new Vec3i(a.X & b, a.Y & b, a.Z & b);
        public static Vec3i operator |(Vec3i a, int b) => new Vec3i(a.X | b, a.Y | b, a.Z | b);
        public static Vec3i operator ^(Vec3i a, int b) => new Vec3i(a.X ^ b, a.Y ^ b, a.Z ^ b);
        public static Vec3i operator >>(Vec3i a, int b) => new Vec3i(a.X >> b, a.Y >> b, a.Z >> b);
        public static Vec3i operator <<(Vec3i a, int b) => new Vec3i(a.X << b, a.Y << b, a.Z << b);

        /// <summary> Returns a new vector containing the smallest components between <paramref name="a"/> and <paramref name="b"/>. </summary>
        public static Vec3i Min(Vec3i a, Vec3i b)
        {
            return new Vec3i(
                a.X < b.X ? a.X : b.X,
                a.Y < b.Y ? a.Y : b.Y,
                a.Z < b.Z ? a.Z : b.Z
            );
        }
        /// <summary> Returns a new vector containing the largest components between <paramref name="a"/> and <paramref name="b"/>. </summary>
        public static Vec3i Max(Vec3i a, Vec3i b)
        {
            return new Vec3i(
                a.X > b.X ? a.X : b.X,
                a.Y > b.Y ? a.Y : b.Y,
                a.Z > b.Z ? a.Z : b.Z
            );
        }
    }
}
