using System;
using AnvilPacker.Data;
using AnvilPacker.Level;

namespace AnvilPacker.Util
{
    public static class DebugTools
    {
        //For debugging purposes - https://cubical.xyz/
        public static void ExportSchematic(RegionBuffer region, string filename, Vec3i? start = null, Vec3i? end = null)
        {
            //TODO: Sponge schematics + https://beta.cubical.xyz/
            if (start == null || end == null) {
                var (minCy, maxCy) = region.GetChunkYExtents();
                int minCx = int.MaxValue, maxCx = int.MinValue;
                int minCz = int.MaxValue, maxCz = int.MinValue;
                foreach (var chunk in region.Chunks.ExceptNull()) {
                    int cx = chunk.X - region.X;
                    int cz = chunk.Z - region.Z;
                    minCx = Math.Min(minCx, cx);
                    maxCx = Math.Max(maxCx, cx);
                    minCz = Math.Min(minCz, cz);
                    maxCz = Math.Max(maxCz, cz);
                }
                start ??= new Vec3i(minCx, minCy, minCz);
                end ??= new Vec3i(maxCx, maxCy, maxCz);
            }
            var palette = MapToLegacyIds(region.Palette);
            var (x1, y1, z1) = start.Value * 16;
            var (x2, y2, z2) = end.Value * 16 + 15;

            int w = x2 - x1 + 1;
            int h = y2 - y1 + 1;
            int d = z2 - z1 + 1;
            var blocks = new byte[w * h * d];
            var data = new byte[w * h * d];

            int index = 0;
            for (int y = y1; y <= y2; y++) {
                for (int z = z1; z <= z2; z++) {
                    for (int x = x1; x <= x2; x++) {
                        int id = 0;

                        var section = region.GetSection(x >> 4, y >> 4, z >> 4);
                        if (section != null) {
                            var block = section.GetBlockId(x & 15, y & 15, z & 15);
                            id = palette[block];
                        }
                        blocks[index] = (byte)(id >> 4);
                        data[index] = (byte)(id & 15);
                        index++;
                    }
                }
            }

            var tag = new CompoundTag();
            tag.SetShort("Width", (short)w);
            tag.SetShort("Height", (short)h);
            tag.SetShort("Length", (short)d);
            tag.SetByteArray("Blocks", blocks);
            tag.SetByteArray("Data", data);

            NbtIO.WriteCompressed(tag, filename);
        }

        /// <summary> Creates a LUT that maps the palette blocks to legacy block ids. </summary>
        public static ushort[] MapToLegacyIds(BlockPalette palette, ushort defaultId = 35 << 4 | 6)
        {
            return palette.ToArray(b => {
                int id = b.Block.Name.Path switch {
                    "air"           => 0,
                    "stone"         => 1,
                    "grass_block"   => 2,
                    "dirt"          => 3,
                    "water"         => 9,
                    "sand"          => 12,
                    _ => 22 //lapis_block
                };
                return (ushort)(id << 4);
            });
        }
    }
}