using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Encoder;
using AnvilPacker.Level;
using AnvilPacker.Util;
using CommandLine;
using CommandLine.Text;
using NLog;

namespace AnvilPacker
{
    //TODO: Look at FLIF's MANIAC encoder
    //https://hbfs.wordpress.com/2011/03/22/compressing-voxel-worlds/
    public class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        static void Main(string[] args)
        {
            Test();
        }

        private static void Test()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var consoleTarget = new NLog.Targets.ConsoleTarget("console");
            consoleTarget.Layout = "[${time} ${level:uppercase=true}] ${logger:shortName=true}: ${replace-newlines:replacement=\n:${message}} ${exception:format=toString}";
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            NLog.LogManager.Configuration = config;

            RegistryLoader.Load();
            //string file = @"C:\Users\Daniel\Desktop\Arquivos2\mc\saves\New World\region\r.-1.0.mca";
            //string file = @"../../../test_data/world_amplified1/region/r.-1.-1.mca";
            string file = @"../../../test_data/world_default1/region/r.0.-1.mca";

            var reader = new AnvilReader(file);

            var serializer = new Level.Versions.v1_16_1.ChunkSerializer();
            var region = new RegionBuffer(32, 32);
            var rng = new Random(4567);

            for (int z = 0; z < 32; z++) {
                for (int x = 0; x < 32; x++) {
                    var tag = reader.Read(x, z);
                    if (tag != null) {
                        var chunk = serializer.Deserialize(tag.GetCompound("Level"));
                        region.SetChunk(x, z, chunk);
/*
                        for (int i = 0; i < 64; i++) {
                            for (int j = 0; j < 8; j++) {
                                var section = chunk.Sections[rng.Next(16)];
                                if (section != null) {
                                    var bx = rng.Next(16);
                                    var by = rng.Next(16);
                                    var bz = rng.Next(16);
                                    section.SetBlock(bx, by, bz, Block.StateRegistry[rng.Next(16384)]);
                                    break;
                                }
                            }
                        }*/
                    }
                }
            }
            using var encRegion = new StreamDataWriter(File.Create("encoded.bin"));

            var sw = Stopwatch.StartNew();
            var encoder = new Encoder.v2.EncoderV2(region);
            
            encoder.Encode(encRegion);
            //Dump(region, "dumped.bin");
            //DumpImages(region, "layers", false);
            Console.WriteLine($"Analysis took {sw.ElapsedMilliseconds}ms");
        }


        private static void Dump(RegionBuffer buf, string filename)
        {
            var splitter = new RegionSplitter(buf, 16);
            var globalPalette = new Dictionary<int, byte>();

            using var fs = File.Create(filename);
            foreach (var unit in splitter.StreamUnits()) {
                var palette = unit.Palette;
                for (int i = 0; i < palette.Length; i++) {
                    globalPalette.TryAdd(palette[i].Id, (byte)globalPalette.Count);
                }
                if (globalPalette.Count > 256) {
                    throw new InvalidOperationException("Palette won't fit in 8 bits");
                }

                foreach (var block in unit.Blocks) {
                    int stateId = palette[block].Id;
                    fs.WriteByte(globalPalette[stateId]);
                }
            }
        }
        private static void DumpImages(RegionBuffer buf, string path, bool gray)
        {
            //ffmpeg -i %d.ppm png/%d.png
            Directory.CreateDirectory(path);

            var header = Encoding.ASCII.GetBytes($"P{(gray?5:6)}\n512 512 255\n");
            int bpp = gray ? 1 : 3;
            byte[] data = new byte[header.Length + (512 * 512) * bpp];
            header.CopyTo(data, 0);

            var palette = new Dictionary<int, int>();

            for (int y = 0; y < 256; y++) {
                for (int z = 0; z < 512; z++) {
                    for (int x = 0; x < 512; x++) {
                        var chunk = buf.GetChunk(x >> 4, z >> 4);
                        var block = chunk == null ? BlockState.Air : chunk.GetBlock(x & 15, y, z & 15);
                        if (!palette.TryGetValue(block.Id, out int color)) {
                            palette[block.Id] = gray ? palette.Count : 
                                                block.Material.Color.ToRgb();
                            if (gray && palette.Count > 256) {
                                throw new NotSupportedException("Region with more than 256 colors");
                            }
                        }
                        int i = header.Length + (x + z * 512) * bpp;
                        if (gray) {
                            data[i] = (byte)color;
                        } else {
                            data[i + 0] = (byte)(color >> 16);
                            data[i + 1] = (byte)(color >> 8);
                            data[i + 2] = (byte)(color >> 0);
                        }
                    }
                }
                File.WriteAllBytes($"{path}/{y}.ppm", data);
            }
        }
    }
}
