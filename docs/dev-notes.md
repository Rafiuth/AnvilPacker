# Dev Notes

## AnvilPack1 overview
This format uses fixed order modeling, symbol ranking and arithmetic coding. It is very simple and reasonably fast.
> TODO: detailed description of how it works?

Brotli is used to compress other metadata, including unprocessed chunk nbt tags. It was choosen because it's better than deflate, and it is available on .NET's BCL.
A few notes about Brotli and ZStandard:
- Brotli creates smaller files at the slowest setting (although it is impractical to use)
- ZStandard creates smaller files at speeds comparable to Brotli

## Reproducible data
Light data and heightmaps can be recomputed using existing block data, as long as block attributes are known.
Attributes of unknown blocks are estimated based on existing data.

Estimating attributes for heightmap is very simple: for a given heightmap, mark all blocks at the given height as opaque. To calculate the height of a given column, simply find the highest non-opaque block.
> TODO: Using a histogram could help improve resilience against wrong values

Estimating light attributes isn't that hard either:
For each block type, keep a histogram of light levels for both opacity and emission.

To populate the emission histogram, for each block, increment the bin if the emitted light is greater than or equal to the maximum light emitted by any of it's neighbors.

Opacity is very similar, except that, instead of checking if the emited light is greater than any of the neighbors, we check if it's less. Different from the emission levels, we only know how much light was blocked, not the actual block opacity.

[insert how it was fixed here]
> Got lazy. For now, it just selects the greatest value that is also greater than a threshold.

## NBT Compression
The main idea for compressing NBT is to learn object structures and only encode field names once.
This is implemented at `AnvilPacker.Encoder.PNbt`, but needs major improvement to be useful. Currently, it only achieves about 5-10% smaller files after compressing with Brotli.

Planar fields like in tree-buf, combined with integer/float compression could help.

- https://github.com/That3Percent/tree-buf
- http://stevehanov.ca/blog/?id=104
- http://stevehanov.ca/blog/cjson.js

## Delta Coding (by world gen)
The idea is to use the input world's seed to regenerate the original world, and encode only the differences.

[insert problems here]

A partial port of the terrain generator is available on the `deltacoding` branch.
Delta encoding using the bare terrain shape could give about 5%-20% smaller files (rough guess, based on the size of the `.schematic`). Not very great given the cost of terrain generation.

The terrain itself isn't the main source of entropy, features such as trees and ores are. It's unfeasible to port the entire generator given the amount of details, which varies per version.
Hooking the Minecraft JAR could do it, but the vanilla generator is just too slow to be practical. (Maybe it's the lighting engine?)

- https://github.com/toolbox4minecraft/amidst
- https://github.com/Cubitect/cubiomes
- https://github.com/hube12/Minemap
- https://github.com/cuberite/cuberite/issues/4759

## Version based Delta Coding
Like git. Could be very useful for backups

[to be further tought]

## Links

- http://mattmahoney.net/dc/dce.html
> Very useful, covers most if not all compression techniques

- https://meiji163.github.io/posts/2021/04/context-tree-weighting/
- https://encode.su/threads/3566-Text-(or-general)-compression-Where-to-begin-What-was-your-learning-path

### Papers

Context tree based
- https://www.researchgate.net/publication/6520506_Lossless_Compression_of_Color_Map_Images_by_Context_Tree_Modeling
> This one seems pretty good but I got stuck on the estimated code length formula. The intermediates will overflow and I don't know how to simplify it.
- https://www.researchgate.net/publication/8084282_Compression_of_map_images_by_multilayer_context_tree_modeling
- https://www.researchgate.net/publication/5576301_Tree_coding_of_bilevel_images

Other
- https://www.researchgate.net/publication/2985776_The_piecewise-constant_image_model
> Didn't fully understand this, seems like it isolates each "blob" and only encodes the edges?
- https://www.researchgate.net/publication/3946257_Progressive_coding_of_palette_images_and_digital_maps
> Based on PPM

Skimmed
- https://www.researchgate.net/publication/257731848_Efficient_and_lossless_compression_of_raster_maps
- https://www.researchgate.net/publication/338959472_On_the_Utilization_of_Reversible_Colour_Transforms_for_Lossless_2-D_Data_Compression
- https://cs.fit.edu/~mmahoney/compression/cs200516.pdf
- https://www.ics.uci.edu/~dan/pubs/TR90-33.pdf