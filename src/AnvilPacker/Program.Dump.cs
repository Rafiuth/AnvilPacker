using System;
using System.IO;
using System.Text;
using AnvilPacker.Cli;
using AnvilPacker.Data;
using AnvilPacker.Level;

namespace AnvilPacker
{
    public partial class Program
    {
        private static void Run(DumpOptions opts)
        {
            if (!File.Exists(opts.Input)) {
                Error($"Input file '{opts.Input}' does not exist.");
            }
            PromptOverwrite(opts.Output, opts.Overwrite, false);

            switch (opts.Type) {
                case DumpType.Nbt:
                    DumpNbt(opts.Input, opts.Output);
                    break;
                case DumpType.NbtPrint:
                    PrintNbt(opts.Input, opts.Output);
                    break;
                case DumpType.RawBlocks:
                    DumpBlocks(opts.Input, opts.Output);
                    break;
                case DumpType.RawChunks:
                    DumpRawChunks(opts.Input, opts.Output);
                    break;
                default: throw new NotImplementedException();
            }
        }

        private static void DumpNbt(string input, string output)
        {
            var root = new CompoundTag();
            root.Set("Content", ReadFileAsNbt(input));

            if (output.EndsWith(".gz")) {
                NbtIO.WriteCompressed(root, output);
            } else {
                using var dw = new DataWriter(File.Create(output));
                NbtIO.Write(root, dw);
            }
        }
        private static void PrintNbt(string input, string output)
        {
            var tag = ReadFileAsNbt(input);
            string text = tag.ToString();

            if (output == null) {
                Console.Write(text);
            } else {
                File.WriteAllText(output, text, Encoding.UTF8);
            }
        }
        private static void DumpBlocks(string input, string output)
        {
            var region = new RegionBuffer();
            region.Load(new WorldInfo(Path.GetDirectoryName(input)), input);
            bool extendedId = region.Palette.Count > 256;
            var (minSy, maxSy) = region.GetChunkYExtents();

            using var fs = new DataWriter(File.Create(output));
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
            region.Load(new WorldInfo(Path.GetDirectoryName(input)), input);
            bool extendedId = region.Palette.Count > 256;
            var (minSy, maxSy) = region.GetChunkYExtents();

            using var fs = new DataWriter(File.Create(output));
            foreach (var chunk in ChunkIterator.Create(region)) {
                for (int y = 0; y < 16; y++) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            var id = chunk.GetBlockId(x, y, z);

                            if (extendedId) {
                                fs.WriteUShortLE(id);
                            } else {
                                fs.WriteByte(id);
                            }
                        }
                    }
                }
            }
        }

        private static NbtTag ReadFileAsNbt(string path)
        {
            var dr = new FileDataReader(path);

            try {
                if (dr.ReadUShortBE() == 0x1F8B) {
                    _logger.Debug("Trying to read gzip compressed NBT tag...");
                    dr.Position = 0;
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
            dr.Dispose();

            try {
                _logger.Debug("Trying to read anvil region...");
                using var region = new RegionReader(path);
                var tags = new ListTag();
                foreach (var (tag, x, z) in region.ReadAll()) {
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