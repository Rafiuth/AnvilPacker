namespace AnvilPacker.Level
{
    //https://minecraft.fandom.com/wiki/Data_version
    public enum DataVersion
    {
        Unknown     = 0,

        v1_12_2     = 1343,

        v1_13_s5    = 1449,  //17w46a: Last version to use numeric block IDs
        v1_13_s6    = 1451,  //17w47a: Blocks are now bit packed and paletted
        v1_14_s6    = 1910,  //18w46a: Directional block opacity
        v1_14_2_pre4 = 1962, //Forced light recomputation, isLightOn added?
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
