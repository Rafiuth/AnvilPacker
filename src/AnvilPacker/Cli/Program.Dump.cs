using System;
using System.IO;
using System.Text;
using AnvilPacker.Cli;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker
{
    public partial class Program
    {
        private static void RunDumper(DumpOptions opts)
        {
            if (!File.Exists(opts.InputPath)) {
                Error($"Input file '{opts.InputPath}' does not exist.");
            }
            PromptOverwrite(opts.OutputPath, opts.Overwrite);

            switch (opts.Type) {
                case DumpType.Nbt:
                    DumpNbt(opts);
                    break;
                case DumpType.NbtPrint:
                    PrintNbt(opts);
                    break;
                case DumpType.RawBlocks:
                    DumpBlocks(opts.InputPath, opts.OutputPath);
                    break;
                case DumpType.RawChunks:
                    DumpRawChunks(opts.InputPath, opts.OutputPath);
                    break;
                default: throw new NotImplementedException();
            }
        }

        private static void DumpNbt(DumpOptions opts)
        {
            var tag = ReadFileAsNbt(opts.InputPath, opts.ExcludeBlocks);
            var root = tag as CompoundTag;
            if (root == null) {
                root = new CompoundTag();
                root.Set("_root", tag);
            }

            string output = opts.OutputPath;
            if (output.EndsWith(".gz")) {
                NbtIO.WriteCompressed(root, output);
            } else {
                using var dw = new DataWriter(File.Create(output));
                NbtIO.Write(root, dw);
            }
        }
        private static void PrintNbt(DumpOptions opts)
        {
            var tag = ReadFileAsNbt(opts.InputPath, opts.ExcludeBlocks);

            string output = opts.OutputPath;
            var sw = output == null ? Console.Out : new StreamWriter(output, false, Encoding.UTF8);

            var printer = new NbtPrinter(sw) {
                Pretty = true
            };
            printer.Print(tag);

            sw.Flush();
            if (output != null) {
                sw.Dispose();
            }
        }
        private static void DumpBlocks(string input, string output)
        {
            var region = new RegionBuffer();
            DebugTools.LoadRegion(region, input);
            bool extendedId = region.Palette.Count > 256;
            var (minSy, maxSy) = region.GetChunkYExtents();

            using var fs = new DataWriter(File.Create(output));
            fs.WriteBool(extendedId);

            for (int y = minSy * 16; y < (maxSy + 1) * 16; y++) {
                for (int z = 0; z < 512; z++) {
                    for (int x = 0; x < 512; x++) {
                        int id = 0;

                        var section = region.GetSection(x >> 4, y >> 4, z >> 4);
                        if (section != null) {
                            id = section.GetBlockId(x & 15, y & 15, z & 15);
                        }

                        if (extendedId) {
                            fs.WriteUShortLE(id);
                        } else {
                            fs.WriteByte(id);
                        }
                    }
                }
            }
            using var sw = new StreamWriter(output + ".palette.txt");
            foreach (var block in region.Palette) {
                sw.WriteLine(block.ToString());
            }
        }

        private static void DumpRawChunks(string input, string output)
        {
            var region = new RegionBuffer();
            DebugTools.LoadRegion(region, input);
            bool extendedId = region.Palette.Count > 256;

            using var fs = new DataWriter(File.Create(output));
            fs.WriteBool(extendedId);
            foreach (var chunk in ChunkIterator.Create(region)) {
                fs.WriteByte(1);
                fs.WriteShortLE(chunk.X & 31);
                fs.WriteShortLE(chunk.Y);
                fs.WriteShortLE(chunk.Z & 31);
            }
            fs.WriteByte(0);

            foreach (var chunk in ChunkIterator.Create(region)) {
                foreach (var id in chunk.Blocks) {
                    if (extendedId) {
                        fs.WriteUShortLE(id);
                    } else {
                        fs.WriteByte(id);
                    }
                }
            }
        }

        private static NbtTag ReadFileAsNbt(string path, bool excludeBlocks)
        {
            using var dr = new DataReader(File.OpenRead(path));

            try {
                if (dr.ReadUShortBE() == 0x1F8B) {
                    _logger.Debug("Trying to read gzip compressed NBT tag...");
                    dr.BaseStream.Position = 0;
                    return NbtIO.ReadCompressed(dr.BaseStream);
                }
            } catch (Exception ex) {
                _logger.Debug(ex, "Failed to read gzipped NBT tag.");
            }

            try {
                _logger.Debug("Trying to read uncompressed NBT tag...");
                dr.Position = 0;
                return NbtIO.Read(dr);
            } catch (Exception ex) {
                _logger.Debug(ex, "Failed to read uncompressed NBT tag.");
            }

            try {
                _logger.Debug("Trying to read anvil region...");
                using var region = new RegionReader(dr.BaseStream, 0, 0);
                var tags = new ListTag();
                foreach (var (tag, x, z) in region.ReadAll()) {
                    if (excludeBlocks && tag["Level"]?["Sections"] is ListTag sects) {
                        foreach (var sect in sects) {
                            if (sect is CompoundTag c) {
                                c.Remove("Blocks");
                                c.Remove("Data");
                                c.Remove("Add");
                                c.Remove("BlockLight");
                                c.Remove("SkyLight");
                            }
                        }
                    }
                    tags.Add(tag);
                }
                return tags;
            } catch (Exception ex) {
                _logger.Debug(ex, "Failed to read anvil region.");
            }
            throw new InvalidOperationException($"Could not read NBT file '{path}'");
        }
    }
}