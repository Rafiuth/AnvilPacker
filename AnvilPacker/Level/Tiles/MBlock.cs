using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public class MBlock : IBlock
    {
        public static ResourceRegistry<MBlock> Registry { get; internal set; }
        public static IndexedMap<MBlockState> StateRegistry { get; internal set; }

        public ResourceName Name { get; init; }
        public int MinStateId { get; init; }
        public int MaxStateId { get; init; }
        public MBlockState DefaultState { get; internal set; }
        public IReadOnlyList<BlockProperty> Properties { get; init; }

        public BlockMaterial Material { get; init; }

        /// <summary> Returns a stream of all possible states. </summary>
        public IEnumerable<MBlockState> GetStates()
        {
            for (int i = MinStateId; i <= MaxStateId; i++) {
                yield return StateRegistry[i];
            }
        }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}