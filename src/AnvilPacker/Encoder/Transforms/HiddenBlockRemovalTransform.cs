﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder.Transforms
{
    /// <summary> Replace occluded blocks with their most frequent neighbor. </summary>
    //XXXX    XXXX
    //XYZX -> XXXX
    //XXXX    XXXX
    public class HiddenBlockRemovalTransform : TransformBase
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary> Specifies how many blocks to search for the replacement. </summary>
        public int Samples = 8;
        /// <summary> Specifies the maximum replacement search distance. </summary>
        public int Radius = 2;
        /// <summary> Keep the neighbor frequency table instead of clearing it for each block. If enabled, fewer samples can be used. Generally improves compression. </summary>
        public bool CummulativeFreqs = true;
        /// <summary> If not null, specifies which blocks can be replaced. </summary>
        public HashSet<Block> Whitelist;

        // S | R | Cum | File Size | time
        //    off      | 1074.439KB| 3s
        //16 | 2 | no  | 542.131KB | 12s
        //24 | 2 | no  | 426.957KB | 13s
        //32 | 2 | no  | 381.824KB | 15s
        // 8 | 2 | yes | 335.672KB | 8s
        //16 | 2 | yes | 335.139KB | 10s

        public HiddenBlockRemovalTransform()
        {
            string[] names = {
                "granite", "polished_granite", "diorite", "polished_diorite", "andesite", 
                "polished_andesite", "bedrock", "gravel", "gold_ore", "iron_ore", "coal_ore", 
                "nether_gold_ore", "redstone_ore", "lapis_ore", "diamond_ore", "emerald_ore", 
                "nether_quartz_ore",
                //this will destroy near surface noisy patterns, but greatly improve compression (1500KB -> 517KB)
                //(seems that omitting stone and only keeping the blocks above will increase file size, bug?)
                "stone", "dirt", "sand", "sandstone"
            };
            int[] legacyIds = {
                //note: Block.Equals() only check for names and properties
                1,  //stone
                3,  //dirt
                12, //sand,
                24, //sandstone
                73, //redstone_ore
                74, //lit_redstone_ore
            };
            Whitelist = new();
            foreach (var name in names) {
                Whitelist.Add(BlockRegistry.GetBlock(name));
            }
            foreach (var id in legacyIds) {
                Whitelist.Add(BlockRegistry.GetLegacyState(id << 4).Block);
            }
        }

        private static readonly Vec3i[] ImmediateNeighbors = {
            new(-1, 0, 0),
            new(+1, 0, 0),
            new(0, -1, 0),
            new(0, +1, 0),
            new(0, 0, -1),
            new(0, 0, +1),
        };

        public override void Apply(RegionBuffer region)
        {
            var neighbors = GetNeighbors();
            var freqs = new int[region.Palette.Count];
            var isOpaque = region.Palette.ToArray(IsOpaque);
            var isWhitelisted = region.Palette.ToArray(s => Whitelist == null || Whitelist.Contains(s.Block));

            var mostFrequent = default(BlockId);
            int changedBlocks = 0;
            int totalBlocks = 0;

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(region)) {
                for (int z = 0; z < 16; z++) {
                    for (int x = 0; x < 16; x++) {
                        var id = chunk.GetBlockIdFast(x, y, z);
                        if (!isWhitelisted[id]) {
                            continue;
                        }
                        if (!CummulativeFreqs) {
                            freqs.Clear();
                        }
                        if (!IsHidden(chunk, x, y, z)) continue;

                        foreach (var pos in neighbors) {
                            int nx = x + pos.X;
                            int ny = y + pos.Y;
                            int nz = z + pos.Z;

                            UpdateFreq(chunk.GetBlockId(nx, ny, nz));
                        }
                        if (mostFrequent != id) {
                            chunk.SetBlockId(x, y, z, mostFrequent);
                            changedBlocks++;
                        }
                    }
                }
                totalBlocks += 256;
            }
            _logger.Debug($"Simplified {changedBlocks}/{totalBlocks} ({changedBlocks * 100 / totalBlocks}%) hidden blocks in {region}");

            //Check if the block is surrounded by opaque blocks
            bool IsHidden(ChunkIterator chunk, int x, int y, int z)
            {
                foreach (var pos in ImmediateNeighbors) {
                    var block = chunk.GetBlockId(x + pos.X, y + pos.Y, z + pos.Z);
                    if (!isOpaque[block]) {
                        return false;
                    }
                    UpdateFreq(block);
                }
                return true;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void UpdateFreq(BlockId id)
            {
                if (++freqs[id] > freqs[mostFrequent]) {
                    mostFrequent = id;
                }
            }
        }

        private bool IsOpaque(BlockState state)
        {
            //Attributes required to be true
            const BlockAttributes AttrMaskT = BlockAttributes.Opaque | BlockAttributes.FullCube;
            //Attributes required to be false
            const BlockAttributes AttrMaskF = BlockAttributes.Translucent;

            var attrs = state.Attributes;
            if ((attrs & (AttrMaskT | AttrMaskF)) != AttrMaskT) {
                return false;
            }
            return true;
        }

        private Vec3i[] GetNeighbors()
        {
            int r = Radius;
            int rs = r * 2 + 1;
            var points = new List<Vec3i>();

            for (int y = -r; y <= r; y++) {
                for (int z = -r; z <= r; z++) {
                    for (int x = -r; x <= r; x++) {
                        points.Add(new Vec3i(x, y, z));
                    }
                }
            }
            var rng = new Random(12345);
            points.Shuffle(rng.Next);
            //TODO: ensure points are at least some distance appart each other (poisson sampling)
            return points.Except(ImmediateNeighbors.Append(new Vec3i(0, 0, 0)))
                         .Take(Samples)
                         .OrderBy(v => ((v.Y + r) * rs + (v.Z + r)) * rs + (v.X + r)) //improve cache coherency
                         .ToArray();
        }
    }
}
