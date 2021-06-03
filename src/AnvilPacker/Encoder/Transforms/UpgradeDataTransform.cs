using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Encoder.Transforms
{
    /// <summary> Reduces the size of the UpgradeData tag. </summary>
    public class UpgradeDataTransform : ReversibleTransform
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static readonly HashSet<string> UpgradeableBlockNames = new(new[] {
            "chest", "trapped_chest", "note_block", "red_bed", "fire", "oak_stairs", "stone_stairs", "cobblestone_stairs", "brick_stairs", 
            "stone_brick_stairs", "nether_brick_stairs", "sandstone_stairs", "spruce_stairs", "birch_stairs", "jungle_stairs", "quartz_stairs", 
            "acacia_stairs", "dark_oak_stairs", "red_sandstone_stairs", "purpur_stairs", "redstone_wire", "oak_fence", "nether_brick_fence", 
            "spruce_fence", "birch_fence", "jungle_fence", "dark_oak_fence", "acacia_fence", "repeater", "iron_bars", "glass_pane", 
            "white_stained_glass_pane", "orange_stained_glass_pane", "magenta_stained_glass_pane", "light_blue_stained_glass_pane", 
            "yellow_stained_glass_pane", "lime_stained_glass_pane", "pink_stained_glass_pane", "gray_stained_glass_pane", "light_gray_stained_glass_pane", 
            "cyan_stained_glass_pane", "purple_stained_glass_pane", "blue_stained_glass_pane", "brown_stained_glass_pane", "green_stained_glass_pane", 
            "red_stained_glass_pane", "black_stained_glass_pane", "vine", "oak_fence_gate", "spruce_fence_gate", "birch_fence_gate", "jungle_fence_gate", 
            "dark_oak_fence_gate", "acacia_fence_gate", "tripwire", "cobblestone_wall", "chorus_plant"
        });

        public override void Apply(RegionBuffer region)
        {
            Transform(region, true);
        }
        public override void Reverse(RegionBuffer region)
        {
            Transform(region, false);
        }

        private void Transform(RegionBuffer region, bool forward)
        {
            var isUpgradeable = region.Palette.ToArray(IsUpgradeable);

            var bits = new BitSet(4096);
            var wrongPreds = new List<int>();

            string inputTagName = forward ? "Indices" : "_WrongIndices";
            string resultTagName = forward ? "_WrongIndices" : "Indices";

            foreach (var chunk in region.Chunks.ExceptNull()) {
                var tag = (CompoundTag)chunk.Opaque["Level"]["UpgradeData"];

                if (tag?[inputTagName] is CompoundTag sections) {
                    TransformSections(chunk, sections);

                    tag.Set(resultTagName, sections);
                    tag.Remove(inputTagName);
                }
            }

            void TransformSections(Chunk chunk, CompoundTag sections)
            {
                foreach (var (key, val) in sections) {
                    int[] indices = val.Value<int[]>();

                    if (indices.Length == 0 && forward) {
                        sections.Remove(key);
                        continue;
                    }
                    if (!int.TryParse(key, out int sy) || chunk.GetSection(sy) is not ChunkSection section) {
                        _logger.Warn($"UpgradeData indices with invalid section Y: '{key}'");
                        continue;
                    }
                    foreach (var idx in indices) {
                        bits[idx] = true;
                    }
                    PredictIndices(section, isUpgradeable, bits, wrongPreds);
                    sections.Set(key, wrongPreds.ToArray());

                    bits.Clear();
                    wrongPreds.Clear();
                }
            }
        }

        private void PredictIndices(ChunkSection section, bool[] isUpgradeable, BitSet actual, List<int> wrongPreds)
        {
            for (int i = 0; i < 4096; i++) {
                bool pred = !IsEdge(i) && isUpgradeable[section.Blocks[i]];
                if (actual[i] != pred) {
                    wrongPreds.Add(i);
                }
            }
            static bool IsEdge(int index)
            {
                int x = (index >> 0) & 15;
                int z = (index >> 4) & 15;
                return x == 0 || z == 0 || x == 15 || z == 15;
            }
        }


        private static bool IsUpgradeable(BlockState state)
        {
            var name = state.Block.Name;
            return name.Namespace == "minecraft" && 
                   UpgradeableBlockNames.Contains(name.Path);
        }
    }
}
