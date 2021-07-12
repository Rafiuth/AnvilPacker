using System;
namespace AnvilPacker.Level
{
    public partial class BlockMaterial
    {
        const MaterialAttributes BM = MaterialAttributes.BlocksMotion;
        const MaterialAttributes F = MaterialAttributes.Flammable;
        const MaterialAttributes L = MaterialAttributes.Liquid;
        const MaterialAttributes SB = MaterialAttributes.SolidBlocking;
        const MaterialAttributes R = MaterialAttributes.Replaceable;
        const MaterialAttributes S = MaterialAttributes.Solid;

        //TODO: update DataExtractor to generate this
        //TODO: load materials dynamically
        public static BlockMaterial Air              { get; } = Reg("air", MapColor.Air, R);
        public static BlockMaterial StructuralAir    { get; } = Reg("structural_air", MapColor.Air, R);
        public static BlockMaterial Portal           { get; } = Reg("portal", MapColor.Air, 0);
        public static BlockMaterial Carpet           { get; } = Reg("carpet", MapColor.Wool, F);
        public static BlockMaterial Plant            { get; } = Reg("plant", MapColor.Plant, 0);
        public static BlockMaterial WaterPlant       { get; } = Reg("water_plant", MapColor.Water, 0);
        public static BlockMaterial ReplaceablePlant { get; } = Reg("replaceable_plant", MapColor.Plant, F | R);
        public static BlockMaterial ReplaceableFireproofPlant { get; } = Reg("replaceable_fireproof_plant", MapColor.Plant, R);
        public static BlockMaterial ReplaceableWaterPlant { get; } = Reg("replaceable_water_plant", MapColor.Water, R);
        public static BlockMaterial Water            { get; } = Reg("water", MapColor.Water, L | R);
        public static BlockMaterial BubbleColumn     { get; } = Reg("bubble_column", MapColor.Water, L | R);
        public static BlockMaterial Lava             { get; } = Reg("lava", MapColor.Fire, L | R);
        public static BlockMaterial SnowLayer        { get; } = Reg("snow_layer", MapColor.Snow, R);
        public static BlockMaterial Fire             { get; } = Reg("fire", MapColor.Air, R);
        public static BlockMaterial Decoration       { get; } = Reg("decoration", MapColor.Air, 0);
        public static BlockMaterial Cobweb           { get; } = Reg("cobweb", MapColor.Wool, S);
        public static BlockMaterial RedstoneLamp     { get; } = Reg("redstone_lamp", MapColor.Air, BM | SB | S);
        public static BlockMaterial Clay             { get; } = Reg("clay", MapColor.Clay, BM | SB | S);
        public static BlockMaterial Dirt             { get; } = Reg("dirt", MapColor.Dirt, BM | SB | S);
        public static BlockMaterial Grass            { get; } = Reg("grass", MapColor.Grass, BM | SB | S);
        public static BlockMaterial DenseIce         { get; } = Reg("dense_ice", MapColor.Ice, BM | SB | S);
        public static BlockMaterial Sand             { get; } = Reg("sand", MapColor.Sand, BM | SB | S);
        public static BlockMaterial Sponge           { get; } = Reg("sponge", MapColor.ColorYellow, BM | SB | S);
        public static BlockMaterial ShulkerBox       { get; } = Reg("shulker_box", MapColor.ColorPurple, BM | SB | S);
        public static BlockMaterial Wood             { get; } = Reg("wood", MapColor.Wood, BM | F | SB | S);
        public static BlockMaterial NetherWood       { get; } = Reg("nether_wood", MapColor.Wood, BM | SB | S);
        public static BlockMaterial BambooSapling    { get; } = Reg("bamboo_sapling", MapColor.Wood, F | SB | S);
        public static BlockMaterial Bamboo           { get; } = Reg("bamboo", MapColor.Wood, BM | F | SB | S);
        public static BlockMaterial Wool             { get; } = Reg("wool", MapColor.Wool, BM | F | SB | S);
        public static BlockMaterial Tnt              { get; } = Reg("tnt", MapColor.Fire, BM | F | S);
        public static BlockMaterial Leaves           { get; } = Reg("leaves", MapColor.Plant, BM | F | S);
        public static BlockMaterial Glass            { get; } = Reg("glass", MapColor.Air, BM | S);
        public static BlockMaterial Ice              { get; } = Reg("ice", MapColor.Ice, BM | S);
        public static BlockMaterial Cactus           { get; } = Reg("cactus", MapColor.Plant, BM | S);
        public static BlockMaterial Stone            { get; } = Reg("stone", MapColor.Stone, BM | SB | S);
        public static BlockMaterial Metal            { get; } = Reg("metal", MapColor.Metal, BM | SB | S);
        public static BlockMaterial SnowBlock        { get; } = Reg("snow_block", MapColor.Snow, BM | SB | S);
        public static BlockMaterial RepairStation    { get; } = Reg("repair_station", MapColor.Metal, BM | SB | S);
        public static BlockMaterial Barrier          { get; } = Reg("barrier", MapColor.Air, BM | SB | S);
        public static BlockMaterial Piston           { get; } = Reg("piston", MapColor.Stone, BM | SB | S);
        public static BlockMaterial Coral            { get; } = Reg("coral", MapColor.Plant, BM | SB | S);
        public static BlockMaterial Vegetable        { get; } = Reg("vegetable", MapColor.Plant, BM | SB | S);
        public static BlockMaterial Egg              { get; } = Reg("egg", MapColor.Plant, BM | SB | S);
        public static BlockMaterial Cake             { get; } = Reg("cake", MapColor.Air, BM | SB | S);
        public static BlockMaterial Amethyst         { get; } = Reg("amethyst", MapColor.ColorPurple, BM | SB | S);
        public static BlockMaterial PowderSnow       { get; } = Reg("powder_snow", MapColor.Snow, SB);
        public static BlockMaterial Sculk            { get; } = Reg("sculk", MapColor.ColorBlack, BM | SB | S);
    }
}
