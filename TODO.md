
# Encoder
- [ ] Block state attribute estimation
  - [x] Heightmap opacity
  - [ ] Light emittance
  - [ ] Light opacity
- [ ] Scheduled ticks
- [ ] Biomes

# Decoder
- [ ] Calculate light data, (1.12.2 produces glitchy results with `LightPopulated=0`)
- [x] Fix legacy_blocks.json, light (opacity) values are wrong

# CLI
- [ ] Encoder/decoder CLI **medium**
- [x] Utility commands (nbt print, region strip/repack)

# Architeture
- [x] Allow BlockStates to be dynamically defined

# Features and Improvements
- [ ] Better NBT compression/Improve PNBT
- [ ] Compress player data/stats (using another archive format would give this for free)
- [ ] Transform to remove monsters/entities
- [ ] PPM/Context tree based format?
- [ ] Write more tests
- [ ] Get more data samples
