using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Archives;
using AnvilPacker.Encoder;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Xunit;

namespace AnvilPacker.Tests
{
    public class EncoderTests
    {
        [Fact]
        public void TestDataIntact()
        {
            RegistryLoader.Load();
            var region = new RegionBuffer();

            var settingsToTest = new[] {
                new RegionEncoderSettings() {
                    HeightmapEncMode = RepDataEncMode.Keep,
                    LightEncMode = RepDataEncMode.Keep,
                    BlockCodec = new BlockCodecSettings.AP1()
                },
                new RegionEncoderSettings() {
                    HeightmapEncMode = RepDataEncMode.Keep,
                    LightEncMode = RepDataEncMode.Keep,
                    BlockCodec = new BlockCodecSettings.Brotli()
                },
            };

            foreach (var regionPath in Directory.GetFiles("Resources/region/", "*.mca")) {
                DebugTools.LoadRegion(region, regionPath, 0, 0);

                foreach (var encSettings in settingsToTest) {
                    var mem = new MemoryDataWriter();

                    var enc = new RegionEncoder(region, encSettings);
                    enc.Encode(mem);

                    mem.Flush();
                    mem.Position = 0;
                    var dr = new DataReader(mem.BaseStream);

                    var decRegion = new RegionBuffer();
                    var decSettings = new RegionDecoderSettings();
                    var dec = new RegionDecoder(decRegion, decSettings);
                    dec.Decode(dr);

                    Assert.True(Verifier.CompareBlocks(region, decRegion));
                    Assert.True(Verifier.CompareLight(region, decRegion));
                    Assert.True(Verifier.CompareMetadata(region, decRegion));

                    //Directory.Create("Resources/out/");
                    //var ext = encSettings.BlockCodec is BlockCodecSettings.AP1 ? "ap1" : "br";
                    //File.WriteAllBytes($"Resources/out/{regionPath}.{ext}", mem.BufferMem.ToArray());
                }
            }
        }

        [Fact]
        public void TestDecoderBackwardCompat()
        {
            RegistryLoader.Load();
            var region = new RegionBuffer();

            foreach (var file in Directory.GetFiles("Resources/region/enc/", "*", SearchOption.AllDirectories)) {
                DebugTools.LoadRegion(region, $"Resources/region/{Path.GetFileNameWithoutExtension(file)}.mca", 0, 0);

                var dr = new DataReader(File.OpenRead(file));

                var decRegion = new RegionBuffer();
                var decSettings = new RegionDecoderSettings();
                var dec = new RegionDecoder(decRegion, decSettings);
                dec.Decode(dr);

                Assert.True(Verifier.CompareBlocks(region, decRegion));
                Assert.True(Verifier.CompareLight(region, decRegion));
                Assert.True(Verifier.CompareMetadata(region, decRegion));
            }
        }

#if false
        [Fact]
        public void GenMin()
        {
            RegistryLoader.Load();
            var region = new RegionBuffer();
            var palette = region.Palette;
            /*palette.Add(BlockRegistry.ParseState("stone"));
            palette.Add(BlockRegistry.ParseState("dirt"));
            palette.Add(BlockRegistry.ParseState("grass_block"));
            palette.Add(BlockRegistry.ParseState("oak_log"));
            palette.Add(BlockRegistry.ParseState("oak_leaves"));*/
            palette.Add(BlockRegistry.GetLegacyState(1 << 4));
            palette.Add(BlockRegistry.GetLegacyState(2 << 4));
            palette.Add(BlockRegistry.GetLegacyState(3 << 4));
            palette.Add(BlockRegistry.GetLegacyState(6 << 4));
            palette.Add(BlockRegistry.GetLegacyState(7 << 4));
            var chunk = new Chunk(0, 0, palette, 0, 4);
            chunk.Opaque = new CompoundTag();
            //chunk.DataVersion = DataVersions.v1_17;

            for (int y = chunk.MinSectionY; y < chunk.MaxSectionY; y++) {
                var section = chunk.GetOrCreateSection(y);
                section.BlockLight = new NibbleArray(4096);
                section.SkyLight = new NibbleArray(4096);
                for (int i = 0; i < 4096; i++) {
                    section.Blocks[i] = (BlockId)(i % palette.Count);
                    section.BlockLight[i] = (byte)(i << 4 | i & 15);
                    section.SkyLight[i] = (byte)(i << 4 | i & 15);
                }
            }
            chunk.GetOrCreateHeightmap(Heightmap.TYPE_LEGACY).Compute(chunk, palette.ToArray(b => b.LightOpacity > 0));

            region.PutChunk(chunk);
            DebugTools.SaveRegion(region, "Test.mca");
        }
        [Fact]
        public void GenLegacySample()
        {
            RegistryLoader.Load();
            var region = new RegionBuffer();
            var palette = region.Palette;

            DebugTools.LoadRegion(region, @"C:\Users\Daniel\AppData\Roaming\.minecraft\saves\mcpworld\region\r.0.0.mca");
            for (int z = 0; z < 32; z++) {
                for (int x = 0; x < 32; x++) {
                    if (x >= 10 &&z>=16 &&x<=14&&z<=20)continue;
                    var chunk = new Chunk(x, z, region.Palette, 0, -1);
                    chunk.GetOrCreateHeightmap(Heightmap.TYPE_LEGACY).Compute(chunk, palette.ToArray(b => b.LightOpacity > 0));
                    chunk.DataVersion = DataVersions.v1_12_2;
                    chunk.Opaque = new CompoundTag();
                    var levelInfo = new CompoundTag();
                    levelInfo.Set("Entities", new ListTag());
                    levelInfo.Set("TileEntities", new ListTag());
                    levelInfo.Set("Entities", new ListTag());
                    levelInfo.SetByte("TerrainPopulated", 1);
                    var biomes = new byte[256];
                    biomes.Fill((byte)1);
                    levelInfo.Set("Biomes", biomes);

                    chunk.Opaque["Level"] = levelInfo;
                    region.PutChunk(chunk);
                }
            }
            DebugTools.SaveRegion(region, "Test.mca");
        }
#endif
    }
}