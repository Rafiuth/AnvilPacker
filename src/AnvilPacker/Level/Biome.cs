using System;

namespace AnvilPacker.Level
{
    public class Biome
    {
        public static Biome[] Registry { get; internal set; } = new Biome[0];

        public static Biome Ocean  => Registry[0];
        public static Biome Plains => Registry[1];
        public static Biome Desert => Registry[2];
        public static Biome Forest => Registry[4];

        public int Id { get; internal set; }
        public string Name { get; init; }

        public float Depth { get; init; }
        public float Scale { get; init; }

        public float Temperature { get; init; }
        public float Downfall { get; init; }

        public static Biome GetFromId(int id)
        {
            if (id >= Registry.Length) {
                return Plains;
            }
            return Registry[id];
        }

        public override string ToString() => $"{Name}";
    }
}