using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public interface IChunkSerializer
    {
        Chunk Deserialize(CompoundTag tag);
        CompoundTag Serialize(Chunk chunk);
    }
}
