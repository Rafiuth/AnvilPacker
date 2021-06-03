using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.PNbt;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using CommandLine;
using CommandLine.Text;
using NLog;

namespace AnvilPacker
{
    public class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {/*
            var tag = NbtIO.ReadCompressed("../../../test_data/nbt/chunks_1.nbt.gz");
            foreach (CompoundTag t in tag.GetList("Chunks")) {
                t.Remove("Biomes");
            }

            var packer = new NbtPacker();
            packer.Add(tag);

            using var mem = new MemoryDataWriter();
            packer.Encode(mem, false);

            var unpacker = new NbtUnpacker(new DataReader(new MemoryStream(mem.Buffer)));
            unpacker.ReadHeader();
            var first = unpacker.Read();

            var br = new BrotliEncoder(10, 22);
            var compressedBuf = new byte[1024 * 1024 * 2];
            //mem.Clear();
            //NbtIO.Write(tag, mem);
            var status = br.Compress(mem.BufferSpan, compressedBuf, out int bytesRead, out int bytesWritten, true);

            return;*/

            var config = new NLog.Config.LoggingConfiguration();
            var consoleTarget = new NLog.Targets.ConsoleTarget("console");
            consoleTarget.Layout = "[${level:uppercase=true} ${logger:shortName=true}] ${replace-newlines:replacement=\n:${message}} ${exception:format=toString}";
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);
            NLog.LogManager.Configuration = config;

            RegistryLoader.Load();

            TestDec();
        }

        private static void TestEnc()
        {
            //string file = @"C:\Users\Daniel\Desktop\Arquivos2\mc\saves\New World\region\r.-1.0.mca";
            //string file = @"../../../test_data/world_amplified1/region/r.-1.-1.mca";
            string file = @"../../../test_data/world_default1/region/r.0.-1.mca";

            var reader = new AnvilReader(file);

            var serializer = new Level.Versions.v1_16.ChunkSerializer();
            var region = new RegionBuffer();
            (region.X, region.Z) = (0 * 32, -1 * 32);
            var rng = new Random(4567);

            for (int z = 0; z < 32; z++) {
                for (int x = 0; x < 32; x++) {
                    var tag = reader.Read(x, z);
                    if (tag != null) {
                        var chunk = serializer.Deserialize(tag.GetCompound("Level"), region.Palette);
                        region.SetChunk(x, z, chunk);
                    }
                }
            }
            Console.WriteLine("Hash: " + Verifier.HashBlocks(region));
            Console.WriteLine("Encoding...");
            var sw = Stopwatch.StartNew();
            if (false) {
                new Encoder.Transforms.HiddenBlockRemovalTransform().Apply(region);
                Console.WriteLine($"Transform took {sw.ElapsedMilliseconds}ms");
                sw.Restart();
            }

            var encoder = new Encoder.v1.EncoderV1(region);
            using var encRegion = new DataWriter(File.Create("encoded.bin"));

            int lastReport = 0;
            encoder.Encode(encRegion, p => {
                int now = Environment.TickCount;
                if (now - lastReport > 250) {
                    lastReport = now;
                    Console.WriteLine($"Encoding... {p * 100:0.0}%");
                }
            });

            //Dump(region, "dumped.bin", false);
            //DumpImages(region, "layers", false);
            Console.WriteLine($"Encoding took {sw.ElapsedMilliseconds}ms");
        }

        private static void TestDec()
        {
            using var encRegion = new DataReader(File.OpenRead("encoded.bin"));

            var sw = Stopwatch.StartNew();
            var dec = new Encoder.v1.DecoderV1();
            int lastReport = 0;
            var region = dec.Decode(encRegion, p => {
                int now = Environment.TickCount;
                if (now - lastReport > 250) {
                    lastReport = now;
                    Console.WriteLine($"Decoding... {p * 100:0.0}%");
                }
            });
            Console.WriteLine($"Decoding took {sw.ElapsedMilliseconds}ms");
            Console.WriteLine("Hash: " + Verifier.HashBlocks(region));
            return;
        }

        private static void Dump(RegionBuffer region, string filename, bool layered)
        {
            if (region.Palette.Count > 256) {
                throw new InvalidOperationException("Palette won't fit in 8 bits");
            }
            using var fs = File.Create(filename);

            if (layered) {
                foreach (var (chunk, y) in ChunkIterator.CreateLayered(region)) {
                    for (int z = 0; z < 16; z++) {
                        for (int x = 0; x < 16; x++) {
                            fs.WriteByte((byte)chunk.GetBlockIdFast(x, y, z));
                        }
                    }
                }
            } else {
                foreach (var section in ChunkIterator.GetSections(region)) {
                    foreach (var block in section.Blocks) {
                        fs.WriteByte((byte)block);
                    }
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

            //new Encoder.Transforms.HiddenBlockRemovalTransform().Apply(buf);

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

