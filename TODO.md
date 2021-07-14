
# Encoder
- [ ] Block state attribute estimation
  - [x] Heightmap opacity
    Basic but seems to be enough
  - [x] Light emission
  - [.] Light opacity
- [ ] Scheduled ticks
- [ ] Biomes

# Decoder
- [x] Calculate light data, (1.12.2 produces glitchy results with `LightPopulated=0`)
- [x] Fix legacy_blocks.json, light (opacity) values are wrong

# CLI
- [x] Encoder/decoder CLI
- [ ] Utility commands (nbt print, region strip/repack)

# Core
- [x] Allow BlockStates to be dynamically defined
- [ ] Enable nullable globally

# Features and Improvements
- [ ] Update blocks.json to 1.17
- [.] Block name aliases
- [ ] Better NBT compression/Improve PNBT
- [ ] Compress player data/stats (using another archive format would give this for free)
- [ ] PPM/Context tree/Context tree weighting/Context mixing based format?
- [ ] Write more tests

# Optimization
- [ ] Lazy NBT tree & NbtWriter
  https://blog.libtorrent.org/2015/03/bdecode-parsers/
- [ ] Experiment with C++ to see if it's worth porting the encoder/decoder
- [x] Use libdeflate in the region writer