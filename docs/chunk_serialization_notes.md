# Notes for Chunk Serialization behavior

- Vanilla's NBT library always return an empty tag, or the default value of a primitive instead of null/throwing exceptions.
- Missing heightmaps will be recalculated.
- Missing light data can be omitted by setting a flag to true "isLightOn" or "LightPopulated" for -1.13 versions.  (info based on decompiled code, req. test)
- Setting flag "shouldSave" to true will cause the chunk to be resaved after it has been loaded at least once.
