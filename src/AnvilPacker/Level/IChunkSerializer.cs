using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Nbt;

namespace AnvilPacker.Level
{
    using static DataVersion;
    public interface IChunkSerializer
    {
        private static readonly IChunkSerializer
            Serializer_v1_2_1 = new Versions.v1_2_1.ChunkSerializer(),
            Serializer_v1_13 = new Versions.v1_13.ChunkSerializer();

        public static readonly (DataVersion MinVersion, DataVersion MaxVersion, IChunkSerializer Serializer)[] KnownSerializers = {
            (v1_17_s17,     v1_17_1,    Serializer_v1_13),
            (v1_13_s6,      v1_17_s8,   Serializer_v1_13),
            (0,             v1_13_s5,   Serializer_v1_2_1)
        };

        /// <param name="tag">The chunk tag stored in the region file.</param>
        /// <param name="palette">Palette in which block IDs should refer to. Note that this method may add new entries to it.</param>
        Chunk Deserialize(CompoundTag tag, BlockPalette palette);
        CompoundTag Serialize(Chunk chunk);
    }
}
