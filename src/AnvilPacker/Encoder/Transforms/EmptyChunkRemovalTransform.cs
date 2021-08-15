using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.Transforms
{
    /// <summary> Removes incomplete/empty chunks. </summary>
    public class EmptyChunkRemovalTransform : TransformBase
    {
        public override void Apply(RegionBuffer region)
        {
            foreach (ref var chunk in region.Chunks.AsSpan()) {
                if (chunk != null && IsEmpty(chunk)) {
                    chunk = null;
                }
            }
        }

        //unchanged since (maybe older)1.14.4-1.17
        private static readonly string[] ChunkStatuses = {
            "empty", 
            "structure_starts", "structure_references", 
            "biomes", "noise", "surface", 
            "carvers", "liquid_carvers", 
            "features", 
            "light", "spawn", "heightmaps", 
            "full"
        };
        private static readonly int ChunkStatus_Surface = Array.IndexOf(ChunkStatuses, "surface");

        private bool IsEmpty(Chunk chunk)
        {
            if (chunk.DataVersion < DataVersion.v1_14_4) {
                return false; //TODO: handle legacy chunks
            }
            var level = chunk.Opaque["Level"] as CompoundTag;
            var status = level?["Status"]?.Value<string>();
            int statusIndex = Array.IndexOf(ChunkStatuses, status);
            if (statusIndex < 0) {
                return chunk.Sections.All(c => c == null);
            }
            if (statusIndex >= ChunkStatus_Surface) {
                return false;
            }
            foreach (var name in new[] { "Lights", "LiquidsToBeTicked", "ToBeTicked", "PostProcessing", "Sections", "TileEntities" }) {
                if (!CheckNullOrPred<ListTag>(name, IsEmptySectionList)) {
                    return false;
                }
            }
            if (!CheckNullOrPred<CompoundTag>("Structures", HasStructures)) {
                return false;
            }
            //shouldn't happen if `status <= surface`
            //if (!CheckNullOrPred<CompoundTag>("CarvingMasks", HasCarvingMask)) {
            //    return false;
            //}
            return true;

            bool CheckNullOrPred<TTag>(string name, Predicate<TTag> pred) where TTag : NbtTag
            {
                var tag = level![name];
                return tag is null || (tag is TTag castedTag && pred(castedTag));
            }
        }

        private bool HasStructures(CompoundTag structs)
        {
            //TODO: Check if it's safe to delete when `Starts.*.id == INVALID` or `References.* == long[0]`
            foreach (var (k, v) in structs) {
                bool known = k == "References" || k == "Starts";
                if (!known || v is CompoundTag { Count: > 0 }) {
                    return false;
                }
            }
            return true;
        }

        private bool IsEmptySectionList(ListTag list)
        {
            if (list == null) {
                return true;
            }
            foreach (var elem in list) {
                if (elem is not ListTag subList || subList.Count > 0) {
                    return false;
                }
            }
            return true;
        }
    }
}