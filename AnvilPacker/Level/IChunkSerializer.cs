using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public interface IChunkSerializer
    {
        /// <param name="palette">Palette in which block IDs refer to. Note that this method may add new entries to this palette.</param>
        Chunk Deserialize(CompoundTag tag, BlockPalette palette);
        CompoundTag Serialize(Chunk chunk);
    }
}
