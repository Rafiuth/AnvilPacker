# Vanilla Behavior notes

# Chunk Serialization
- When a tag does not exist in a compound tag, an empty tag or the default(T) is returned instead.
- Missing heightmaps will be recalculated.
- Missing light will be recalculated if `isLightOn=0` (or `LightPopulated=0` in legacy versions). (modern version: guess, req. test)
- Setting `shouldSave=1` will cause the chunk to be resaved after it has been loaded at least once.
- Unused palette entries are removed during chunk serialization.

## 1.17
- World height is stored in data packs. (quite annoying and doesn't feel very reliable)

- Heightmaps:
  The number of bits per element varies according to world height:
  `bitsPerElem = ceilLog2(worldHeight + 1)` 
  `height[x, z] = rawValue[x, z] + worldMinBuildHeight`

- Entities are now stored in separated region files, under `entities/` folder. (why?)


## Legacy versions (-1.12.2)
- BlockLight, SkyLight and HeightMap are required, chunk deserialization will fail if they are missing (HeightMap will only result in a warning).
- Region file sizes must always be a multiple of 4096 bytes, otherwise the header will get silently overwritten by zeroes, causing chunks to be regenerated. (1.12.2)
- Recomputed block light is glitchy even when `LightPopulated=0`

## TBD
- Four Shades of Ticks: "ToBeTicked", "LiquidsToBeTicked" / "TileTicks", "LiquidTicks"
Guess: The first two seem to be used after an world has been upgrated or generated.

"UpgradeData" "Lights" "CarvingMasks" "PostProcessing"

# WorldGen

The base terrain generator seem to be unchanged between (maybe older) 1.8-1.17. Only small features, such as biomes, caves and other details changed.


## Useful Links
https://github.com/coderbot16/i73/blob/master/I73%20Noise%20Generation