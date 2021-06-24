using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    using static DataVersions;
    public interface IChunkSerializer
    {
        public static readonly (int MinVersion, int MaxVersion, IChunkSerializer Serializer)[] KnownSerializers = {
            (v1_13_s6,    v1_17,    new Versions.v1_13.ChunkSerializer()),
            (0,          v1_13_s5,  new Versions.v1_2_1.ChunkSerializer())
        };

        /// <param name="tag">The "Level" tag.</param>
        /// <param name="palette">Palette in which block IDs refer to. Note that this method may add new entries to this palette.</param>
        Chunk Deserialize(CompoundTag tag, BlockPalette palette);
        CompoundTag Serialize(Chunk chunk);
    }
    public static class DataVersions
    {
        //https://minecraft.fandom.com/wiki/Data_version
        public const int v1_12_2    = 1343;

        public const int v1_13_s5   = 1449; //17w46a: Last version to use numeric block IDs
        public const int v1_13_s6   = 1451; //17w47a: Blocks are now bit packed and paletted
        public const int v1_16_s13  = 2529; //20w17a: Bit storage is now sparse

        public const int v1_16_5    = 2586;
        public const int v1_17      = 2724;

        public const int BeforeFlattening = v1_13_s5;
        public const int AfterFlattening  = v1_13_s6;
    }
}
