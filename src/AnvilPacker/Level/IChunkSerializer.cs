using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnvilPacker.Data;

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
    public enum DataVersion
    {
        Unknown     = 0,

        //https://minecraft.fandom.com/wiki/Data_version
        v1_12_2     = 1343,

        v1_13_s5    = 1449,  //17w46a: Last version to use numeric block IDs
        v1_13_s6    = 1451,  //17w47a: Blocks are now bit packed and paletted
        v1_14_s6    = 1910,  //18w46a: Directional block opacity
        v1_14_2_pre4 = 1962, //Forced light recomputation, isLightOn added
        v1_14_4     = 1976,

        v1_16_s13   = 2529,  //20w17a: Bit storage is now sparse

        v1_16_5     = 2586,

        v1_17_s8    = 2692,  //21w05b
        v1_17_s9    = 2694,  //21w06a: World height increased to [-64..320)
        v1_17_s17   = 2709,  //21w15a: World height decreased to [0..256)

        v1_17       = 2724,
        v1_17_1     = 2730,


        BeforeFlattening = v1_13_s5,
        AfterFlattening  = v1_13_s6,

        ForcedLightRecalc = v1_14_2_pre4,
    }
}
