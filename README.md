# AnvilPacker

AnvilPacker is a command line tool for compressing Minecraft worlds for long term storage. It is currently capable of producing files that are about 3 to 100 times smaller, at good speed.

# Features
- Supports Java Edition versions `1.2.1` up to `1.17.1` (except 1.17+ worlds with increased height)
- Lossless and lossy compression
- Can remove redundant data such as lighting and heightmaps
- Solid compression of small files (playerdata, datapacks, etc.)

# Benchmarks

| World                 | Method    | Size      | Ratio | Enc Time | Dec Time |
|:-----                 |:--------- | ---------:| -----:| --------:| --------:|
| Ariane 5              | None      | 260.709MB | 0.00  | n/a      | n/a      |
| Ariane 5              | LZMA2:7   | 46.846MB  | 5.57  | 22s      | 2s       |
| Ariane 5              | AP1-S     | 2.479MB   | 105.17| 8s       | 10s      |
| EF-cyberpunk-1.12     | None      | 232.941MB | 0.00  | n/a      | n/a      |
| EF-cyberpunk-1.12     | LZMA2:7   | 46.797MB  | 4.98  | 19s      | 2s       |
| EF-cyberpunk-1.12     | AP1-S     | 4.131MB   | 56.39 | 15s      | 23s      |
| vanilla-1 v1.12.2     | None      | 99.613MB  | 0.00  | n/a      | n/a      |
| vanilla-1 v1.12.2     | LZMA2:7   | 66.384MB  | 1.50  | 13s      | 3s       |
| vanilla-1 v1.12.2     | AP1-S     | 24.266MB  | 4.11  | 30s      | 32s      |
| vanilla-1 v1.16.5     | None      | 112.372MB | 0.00  | n/a      | n/a      |
| vanilla-1 v1.16.5     | LZMA2:7   | 77.221MB  | 1.46  | 14s      | 3s       |
| vanilla-1 v1.16.5     | AP1-S     | 18.862MB  | 5.96  | 17s      | 22s      |
|Witchcraft and Wizardry| None      | 794.268MB | 0.00  | n/a      | n/a      |
|Witchcraft and Wizardry| LZMA2:7   | 177.059MB | 4.49  | 65s      | 10s      |
|Witchcraft and Wizardry| AP1-S     | 15.427MB  | 51.49 | 34s      | 51s      |

LZMA2:7 files were generated by `7-Zip 19.00`, with compression level set to 7 (normal).
AP1-S files were generated by `AnvilPacker v0.9.5-940465ec`, with `--preset smaller`

## Dataset
- vanilla-1: 2048x2048 blocks of the seed `1963849804999169910`
- [Ariane 5](https://www.curseforge.com/minecraft/worlds/ariane-5-world/files)
- [EF-cyberpunk](https://www.planetminecraft.com/project/cyberpunk-project-timelapse/)
- [Witchcraft and Wizardry](https://www.planetminecraft.com/project/harry-potter-adventure-map-3347878/)

# Usage
See [USAGE.md](https://github.com/Rafiuth/AnvilPacker/blob/main/USAGE.md)

# How it works
AnvilPacker uses a custom compression algorithm to compress block data, which is based on context modeling and arithmetic coding. It can deliver significantly smaller files, and is generally faster than general purpose algorithms, even at slower settings.

# Related Projects
- [mc_recompress](https://github.com/pruby/mc_recompress)
- [PackSquash](https://github.com/ComunidadAylas/PackSquash)
