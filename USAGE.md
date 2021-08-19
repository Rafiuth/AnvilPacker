# Usage
Prebuilt binaries are available in [releases](https://github.com/Rafiuth/AnvilPacker/releases).

The CLI syntax is:
```
AnvilPacker <command> [options]
```

## Examples
Compressing a world using the default settings:
```
AnvilPacker pack -i input_world/ -o compressed_world.apw
```

Compressing a world using brotli and removing light data:
```
AnvilPacker pack -i input_world/ -o compressed_world.apw -e block_codec=brotli,light_enc_mode=strip
```

Decompressing a world:
```
AnvilPacker unpack -i compressed_world.apw -o decompressed_world/
```

## Pack command
Compresses a given world.

Options:
```md
-i|--input <path>           Input world path.
-o|--output <path>          Output file path. If this contain a extension,
                            the output will be written to ZIP file. Otherwise,
                            it will be written to a plain directory.
                            The recommended extension is `.apw`

Optional:
-y|--overwrite              Overwrite the output path if it already exists. Default: false
--threads <count>           Number of threads to use during processing.
                            Higher values demands more memory.
                            Default: System's CPU thread count
--log-level <level>         Sets the log level. Can be one of the following:
                            trace|debug|info|warn|error|fatal.
                            Default: info
--log-file <path>           When set, the log will be also written to
                            the specified file.
--preset <name>             Specifies which preset to use.
--transform-pipe <pipe>     A transform pipe string to apply in regions.
-e|--encoder-opts <opts>    Specifies the encoder settings.
--no-blobs                  Disable solid compression of small files.

Planned:
--add-transforms <pipe>     Adds a transform to the existing (preset) pipeline.
--combine-only <opts>       Compress by combining chunk tags together, 
                            without any further processing.
--verify                    Verify that regions were encoded correctly.
```

## Unpack command
Decompresses a given world.

Options:
```md
-i|--input <path>           Compressed input file/directory path.
-o|--output <path>          Output world path.

Optional:
-y|--overwrite              Overwrite the output path if it already exists.
--threads <count>           Number of threads to use during processing.
                            Higher values demands more memory.
                            Default: System's CPU thread count
--dont-lit                  Don't precompute light data for chunks 
                            targeting version >= 1.14.2.
                            Only affects chunks whose light was stripped.
```

---

## Setting notation
Some settings are passed as objects notated in a JSON-like syntax. The main differences are:
- Properties are not quotted.
- Objects can be explicitly typed: `object_type{property=value,...}` or `obj_property=obj_type`. The later case is ambiguous with unquoted strings, so the actual value will depend on the property type.
- Strings can be optionally delimited by either `'` or `"`. It supports the following escape codes: `\n \r \t \" \' \\`.
- Whitespaces between tokens are not allowed _yet_.

## Encoder Presets
Predefined encoder settings, taken by `--preset`:

### **fast**
Fast compression and decompression, files are about 50% smaller than the original uncompressed world.
- Transform pipe: `remove_empty_chunks,simplify_upgrade_data`
- Encoder opts: `block_codec=brotli{quality=5,window_size=20},meta_brotli_quality=5,meta_brotli_window_size=20`

### **default**
Compromise between speed and size. Decompression takes about the same time as compression.
- Transform pipe: `remove_empty_chunks,simplify_upgrade_data`
- Encoder opts: `block_codec=ap1`

### **smaller**
Produces smaller files (about ~15-30% than default) by removing light data and heightmaps, decompression is slower compared to `default`.
- Transform pipe: `remove_empty_chunks,simplify_upgrade_data`
- Encoder opts: `block_codec=ap1,light_enc_mode=strip,heightmap_enc_mode=strip`

### **lossy**
Produces smaller files (about ~70-250% than default) by removing light data, heightmaps and hidden blocks. Both compression and decompression are slower compared to `default`.
- Transform pipe: `remove_empty_chunks,simplify_upgrade_data,remove_hidden_blocks`
- Encoder opts: `block_codec=ap1,light_enc_mode=strip,heightmap_enc_mode=strip`

## Encoder Options
Fields of the object passed to `--encoder-opts`, represented using [Setting Notations](#Setting_notation).

| Setting       | Type | Default | Description |
| -------       | ---- | ------- | ----------- |
| block_codec   | BlockCodec | ap1 | Specifies which block codec to use. |
| light_enc_mode| RepDataEncMode | keep | Specififes how to encode light data. |
| heightmap_enc_mode | RepDataEncMode | strip | Specifies how to encode heightmaps. |
| meta_brotli_quality  | int  | 8       | Brotli compression quality for metadata compression, where 0 is none/fastest and 11 is best/slowest. |
| meta_brotli_window_size | int | 22    | Brotli sliding window size for metadata compression, in base 2 logarithm. Range: 10-24 |

### RepDataEncMode
Reproducible data (lighting and heightmaps) can be encoded in one of the following ways:
- **strip**: Remove it completely. The decoder will attempt to reconstruct it, or leave it for the game to recompute if possible. Not recommended for modded worlds.
- **keep**: Don't touch it, just compress it with Brotli. This is the safest option. In normal worlds, light data takes about 15-20% of the file size, and heightmaps 5-10%.
- **delta**: Encode differences from the data the decoder would reconstruct. This may result in smaller files and is lossless, but **there is no guarantee that future versions will decode it correctly**. Use it at your own risk.
- **auto**: If all blocks are known, this option behaves as `strip`; otherwise, as `keep`.

To recompute this data, the decoder needs to know block attributes, such as light emission/opacity, heightmap opacity, and shape for directional lighting. They are sourced from either a registry of known vanilla blocks, or estimated based on existing data, by the encoder. (estimation does not support directional lighting)

In some cases, the estimated values will be inaccurate, which may result in wrong or glitchy lighting in the decoded world. If the target world version is 1.14.2+, the decoder can be configured to leave the light data to be recomputed by the game using `--dont-lit`. This may degrade loading speed, but will always produce correct results. In this case, [Starlight](https://github.com/Tuinity/Starlight) could be used to speedup load times.

## Solid Compression
By default, some files are concatenated together, and compressed into "blobs". This can be disabled with the `--no-blobs` switch.

The following conditions determine whether a file will be solid compressed:
- Must be smaller than 128KB
- Extension must be one of: `.json .dat .dat_old .nbt .mcfunction .mcmeta .yml .yaml .toml`

Compressed NBT files (or any gzipped file) are uncompressed before they are recompressed.

Notes:
- The current implementation generates at least one blob per thread.
- The order files are aggregated is non-deterministic; compressing the same input more than once may result in different file sizes, but the decompressed content should be the same.

## Block Codecs

### **ap1**
Compresses the block data using the AnvilPacker v1 algorithm.
It takes roughly the same amount of time and memory to both encode and decode this format.

| Setting       | Type | Default | Description |
| -------       | ---- | ------- | ----------- |
| context_bits  | int  | 13      | Number of contexts, in base 2 logarithm. |
| neighbors     | Vec3i[]| [{x:-1},{y:-1},{z:-1}] | Relative coords of blocks to be used as the context. At most 4 coords are supported. |

Memory usage is about `(172 + palette_size * 6) * 2^context_bits` bytes,
complexity is `O(num_blocks * palette_size)` in the worst case.

### **brotli**
Compresses the raw block data using the [Brotli](https://en.wikipedia.org/wiki/Brotli) algorithm.
Brotli is better for simpler worlds, like super flat with small builds. It is very fast to decompress, but about 2x worse with normal worlds, in comparison to AP1.
Quality levels above 9 are significantly better than lower levels, but too slow for practical use.

| Setting       | Type | Default | Description |
| -------       | ---- | ------- | ----------- |
| quality       | int  | 6       | Compression quality, where 0 is none/fastest and 11 is best/slowest |
| window_size   | int  | 22      | Sliding window size, in base 2 logarithm. Range: 10-24 |

## Transforms
Transforms are used to help increase compression efficiency by converting chunk data into simpler representations.

A pipeline is simply a list of transforms, which are applied in order on a region.
Example: `remove_empty_chunks,remove_hidden_blocks{radius=2,whitelist=[stone,dirt,sand,1,3,12]}`

Available transforms are listed below.

### **remove_hidden_blocks**
This is the main lossy transform, it effectively removes ores and other features by replacing blocks that are surrounded by opaque blocks, with one that appears several times around it.
It generally improves compression by about 1.5-3 times in normal worlds.

| Setting   | Type  | Default | Description |
| -------   | ----  | ------- | ----------- |
| radius    | int   | 2       | Max distance for the replacement block search. |
| samples   | int   | 8       | Number of samples to use in the replacement block search. |
| whitelist | Block[]? | stone, dirt, ores | Specifies which blocks can be replaced. Any block will be replaced if this is `null`. |
| cummulative_freqs | bool  | true | This virtually expands the search radius by accumulating frequencies of replacement blocks. |

Type: irreversible, lossy
### **remove_empty_chunks**
Removes empty/incomplete chunks when `Level.Status <= "surface"` and no other data is present.

Type: irreversible, lossless
### **simplify_upgrade_data**
Simplifies the `Level.UpgradeData` tag. This significantly reduces the metadata size of partially-upgraded worlds.

Type: reversible, lossless
### **sort_palette**
Sorts region palettes using the specified mode.

| Setting | Type   | Default | Description |
| ------- | ----   | ------- | ----------- |
| mode    |SortMode|frequency| Specifies how to sort the palette. |

#### SortMode
- **frequency**: Sort using block frequencies, in descending order.

Type: irreversible, lossless