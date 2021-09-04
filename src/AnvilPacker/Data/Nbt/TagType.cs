namespace AnvilPacker.Data.Nbt
{
    public enum TagType : byte
    {
        End       = 0,
        Byte      = 1,
        Short     = 2,
        Int       = 3,
        Long      = 4,
        Float     = 5,
        Double    = 6,
        ByteArray = 7,
        String    = 8,
        List      = 9,
        Compound  = 10,
        IntArray  = 11,
        LongArray = 12
    }
    public static class TagTypes
    {
        /// <summary> Returns whether the specified type is a numeric primitive. </summary>
        public static bool IsNumber(this TagType type)
        {
            return type is
                TagType.Byte or
                TagType.Short or
                TagType.Int or
                TagType.Long or
                TagType.Float or
                TagType.Double;
        }

        /// <summary> Returns whether the specified type is a primitive (number or string). </summary>
        public static bool IsPrimitive(this TagType type)
        {
            return type.IsNumber() || type == TagType.String;
        }
        public static bool IsArray(this TagType type)
        {
            return type is
                TagType.ByteArray or
                TagType.IntArray or
                TagType.LongArray;
        }
    }
}