using AnvilPacker.Level;

namespace AnvilPacker.Encoder
{
    internal enum BlockFlags
    {
        None        = 0,

        Legacy      = 1 << 0,
    }
    internal static class BlockFlagsEx
    {
        public static BlockFlags FromState(BlockState state)
        {
            var flags = BlockFlags.None;
            if (state.HasAttrib(BlockAttributes.Legacy)) {
                flags |= BlockFlags.Legacy;
            }
            return flags;
        }
    }
}