# Vanilla's Chunk Serialization behavior

- When a tag does not exist in a compound tag, an empty tag or the default(T) is returned instead.
- Missing heightmaps will be recalculated.
- Missing light data can be omitted by setting a flag to true "isLightOn" or "LightPopulated" for -1.13 versions.  (guess, req. test)
- Setting flag "shouldSave" to true will cause the chunk to be resaved after it has been loaded at least once.
- Unused palette entries are removed during chunk serialization.

## Legacy serializers (-1.12.2)
- BlockLight, SkyLight and HeightMap are required, chunk deserialization will fail they are missing (HeightMap will only result in a warning).

## TBD
- The 4 tile tick lists: "ToBeTicked", "LiquidsToBeTicked" / "TileTicks", "LiquidTicks"
Guess: The first two seem to be used after an world has been upgrated or generated.

"UpgradeData" "Lights" "CarvingMasks" "PostProcessing"