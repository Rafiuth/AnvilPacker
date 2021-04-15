using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level.Serializer
{
    public interface IChunkSerializer
    {
        ChunkBase Deserialize(CompoundTag tag);
        CompoundTag Serialize(ChunkBase chunk);
    }
}
