using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level
{
    public interface IChunkSerializer
    {
        ChunkBase CreateChunk(int x, int z);

        ChunkBase Deserialize(CompoundTag tag);
        CompoundTag Serialize(ChunkBase chunk);
    }
}
