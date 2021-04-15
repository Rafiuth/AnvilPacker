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
    //TODO: Octree/greedy encoder?
    //https://hbfs.wordpress.com/2011/03/22/compressing-voxel-worlds/
    //https://marknelson.us/posts/1996/09/01/bwt.html
    //https://en.wikipedia.org/wiki/Wavelet_Tree
    //http://www.wseas.us/e-library/conferences/2007tenerife/papers/572-649.pdf
    //https://github.com/RvanderLaan/SVDAG-Compression
    //https://github.com/Eisenwave/voxel-compression-docs
    //http://www.cse.chalmers.se/~uffe/HighResolutionSparseVoxelDAGs.pdf

    //https://www.researchgate.net/publication/303597840_Geometry_and_Attribute_Compression_for_Voxel_Scenes#pfb
    //https://en.wikipedia.org/wiki/Zstandard

    //https://github.com/caoscott/SReC

    //https://github.com/mayuki/Cocona
    //https://github.com/spectresystems/spectre.console
    public class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        
        static void Main(string[] args)
        {
            Test();
        }

        private static void Test()
        {
            MRegistryLoader.Load();
            string file = @"C:\Users\Daniel\Desktop\Arquivos2\mc\saves\New World\region\r.-1.0.mca";
            //string file = @"worldtest2/region/r.1.-2.mca";
            var reader = new AnvilReader(file);

            var serializer = new Level.Versions.v1_16_1.ChunkSerializer();
            var region = new RegionBuffer(32, 32);

            for (int z = 0; z < 32; z++) {
                for (int x = 0; x < 32; x++) {
                    var tag = reader.Read(x, z);
                    if (tag != null) {
                        var chunk = serializer.Deserialize(tag.GetCompound("Level"));
                        region.SetChunk(x, z, chunk);
                    }
                }
            }
            var ctx = new EncoderContext() {
                OutStream = new MemoryStream()
            };
            for (int i = 0; i < 16; i++) {
                var sw = Stopwatch.StartNew();
                var encoder = new RegionEncoder(ctx, region);
                encoder.Encode();
                Console.WriteLine($"Analysis took {sw.ElapsedMilliseconds}ms");
            }
        }
    }
}
