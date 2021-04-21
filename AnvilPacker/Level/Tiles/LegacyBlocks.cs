using System;

namespace AnvilPacker.Level
{
    public class LegacyBlocks
    {
        public static Block GetBlockFromId(int id)
        {
            throw new NotImplementedException();
        }
        public static Block GetBlockFromName(string name)
        {
            throw new NotImplementedException();
        }

        /// <summary> Returns the block state for the packed id </summary>
        /// <param name="id">The block state id, packed as <c> blockId << 4 | state </c> </param>
        public static BlockState GetStateFromId(int id)
        {
            throw new NotImplementedException();
        }

        public static int GetBlockId(Block block)
        {
            throw new NotImplementedException();
        }
        public static int GetStateId(BlockState state)
        {
            throw new NotImplementedException();
        }
    }
}