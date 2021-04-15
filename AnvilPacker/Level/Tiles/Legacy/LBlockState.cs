using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level.Versions
{
    /// <summary> Implements the legacy block state implemented in -1.13 </summary>
    public partial class LBlockState : IBlockState, IEquatable<LBlockState>
    {
        public static LBlockState Air { get; } = STATE_CACHE[0];

        public int Id { get; }

        public int BlockId => Id >> 4;
        public int BlockData => Id & 15;

        /// <summary> Block state name, may be null. </summary>
        public string Name { get; internal set; }

        public BlockMaterial Material => Id < 16 ? BlockMaterial.Air : BlockMaterial.Aggregate;

        private LBlockState(int id)
        {
            Id = id;
        }

        public static LBlockState Get(int id)
        {
            if ((uint)id <= STATE_CACHE.Length) {
                return STATE_CACHE[id];
            }
            return new LBlockState(id);
        }

        public bool Equals(LBlockState other)   => other.Id == Id;
        public override bool Equals(object obj) => obj is LBlockState mb && Equals(mb);
        public override int GetHashCode()       => Id;

        public override string ToString() => $"{BlockId}:{BlockData} ({Name ?? "unknown"})";
    }
    public class LBlock : IBlock
    {
        public static LBlock GetFromId(int id)
        {
            return new LBlock();
        }
        public static LBlock GetFromName(string id)
        {
            return new LBlock();
        }
    }
}
