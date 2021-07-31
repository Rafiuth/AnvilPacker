# AnvilPacker

AnvilPacker is a tool for compressing Minecraft worlds for long term storage. It is currently capable of producing files that are 3 to 70 times smaller, at good speed.

# Features
- Supports versions Java Edition `1.2.1` up to `1.17.1` (except 1.17+ worlds with increased height)
- Lossless and lossy-ish compression
- As fast as LZMA2 on the maximum level
- Lighting and heightmaps can be stripped and recomputed during decode
- Solid compression of small files (playerdata, datapacks, etc.)

# Benchmarks

| World                 | Method       | Size      | Ratio | Enc Time | Dec Time |
| -----                 | ------       | ----      | ----- | -------- | -------- |
| vanilla1a             | None         | 102,003KB | 1.00  | n/a      | n/a      |
| vanilla1a             | 7z LZMA2:26  | 67,873KB  | 1.45  | 21s      | 3s       |
| vanilla1a             | AnvilPacker  | 28,643KB  | 3.56  | 35s      | 31s      |
| vanilla1b             | None         | 115,068KB | 1.00  | n/a      | n/a      |
| vanilla1b             | 7z LZMA2:26  | 78,990KB  | 1.45  | 23s      | 4s       |
| vanilla1b             | AnvilPacker  | 23,326KB  | 4.93  | 20s      | 19s      |
| ImperialCity v14.1    | None         | 205,943KB | 1.00  | n/a      | n/a      |
| ImperialCity v14.1    | 7z LZMA2:26  | 121,583KB | 1.69  | 41s      | 6s       |
| ImperialCity v14.1    | AnvilPacker  | 37,349KB  | 5.51  | 36s      | 29s      |
|Witchcraft and Wizardry| None         | 813,330KB | 1.00  | n/a      | n/a      |
|Witchcraft and Wizardry| 7z LZMA2:26  | 180,540KB | 4.50  | 1m 28s   | 22s      |
|Witchcraft and Wizardry| AnvilPacker  | 25,897KB  | 31.40 | 49s      | 52s      |
| Ariane 5              | None         | 266,966KB | 1.00  | n/a      | n/a      |
| Ariane 5              | 7z LZMA2:26  | 47,853KB  | 5.57  | 31s      | 3s       |
| Ariane 5              | AnvilPacker  | 3,604KB   | 74.07 | 10s      | 11s      |
| EF-cyberpunk-1.12     | None         | 238,530KB | 1.00  | n/a      | n/a      |
| EF-cyberpunk-1.12     | 7z LZMA2:26  | 47,689KB  | 5.00  | 28s      | 3s       |
| EF-cyberpunk-1.12     | AnvilPacker  | 11,014KB  | 21.65 | 20s      | 17s      |

** Tests ran on a potato laptop, equipped with an Intel i3 running at 2.3GHz, randomly thermally throttled to 1.5GHz.

## Dataset
- vanilla1a: 2048x2048 blocks of the seed 1963849804999169910, freshly generated in v1.12.2
- vanilla1b: 2048x2048 blocks of the seed 1963849804999169910, freshly generated in v1.16.5
- [ImperialCity](https://www.planetminecraft.com/project/monumental-imperial-city/)
- [Witchcraft and Wizardry](https://www.planetminecraft.com/project/harry-potter-adventure-map-3347878/)
- [Ariane 5](https://www.curseforge.com/minecraft/worlds/ariane-5-world/files)
- [EF-cyberpunk](https://www.planetminecraft.com/project/cyberpunk-project-timelapse/)

# Usage
See [USAGE.md](https://github.com/Rafiuth/AnvilPacker/blob/main/USAGE.md)

# How it works
AnvilPacker uses a custom compression algorithm to compress block data, which is based on context modeling and arithmetic coding. It can deliver significantly smaller files, and is generally faster than general purpose algorithms, even at slower settings.

# Planned
- Ingame decoder mod
- Support for BE/PE worlds

# Related Projects
- [mc_recompress](https://github.com/pruby/mc_recompress)
- [PackSquach](https://github.com/ComunidadAylas/PackSquash)
