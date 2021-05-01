using System;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Level
{
    //typedef ushort BlockId;

    public readonly struct BlockId : IEquatable<BlockId>
    {
        public readonly ushort Value;

        public BlockId(ushort value)
        {
            Value = value;
        }

        public readonly bool Equals(BlockId other) => other.Value == Value;
        public override int GetHashCode() => Value;
        public override bool Equals(object obj) => obj is BlockId bid && Equals(bid);

        public override string ToString() => Value.ToString();

        public static implicit operator BlockId(ushort id) => new(id);
        public static implicit operator ushort(BlockId id) => id.Value;

        public static explicit operator BlockId(int id) => new((ushort)id);
        public static implicit operator int(BlockId id) => id.Value;
    }
}