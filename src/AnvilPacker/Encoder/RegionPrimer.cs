using System;
using System.Linq;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    /// <summary> Responsible of reconstruction of non critical data in decoded regions. </summary>
    public class RegionPrimer
    {
        public RegionBuffer Region { get; }
        public EstimatedBlockAttribs EstimatedBlockAttribs { get; }

        public RegionPrimer(RegionBuffer region, EstimatedBlockAttribs estimatedBlockAttribs)
        {
            Region = region;
            EstimatedBlockAttribs = estimatedBlockAttribs;
        }

        public void Prime()
        {
            PrimeHeightmaps();
            PrimeLights();
        }

        private void PrimeHeightmaps()
        {
            var attribs = EstimatedBlockAttribs.HeightmapAttribs;

            foreach (var (type, isOpaque) in attribs.OpacityMap) {
                var computer = new HeightmapComputer(Region, type, isOpaque);

                foreach (var chunk in Region.Chunks.ExceptNull()) {
                    if (NeedsHeightmap(chunk, type)) {
                        computer.Compute(chunk);
                    }
                }
            }
        }
        private bool NeedsHeightmap(Chunk chunk, string type)
        {
            if (DataVersions.IsBeforeFlattening(chunk.DataVersion)) {
                return type == Heightmap.TYPE_LEGACY;
            }
            var status = chunk.Opaque["Level"]?["Status"]?.Value<string>();
            bool statusComplete = status is "full" or "heightmaps" or "spawn" or "light";
            return statusComplete && !type.EndsWith("_WG");
        }

        private void PrimeLights()
        {
            foreach (var chunk in Region.Chunks.ExceptNull()) {
                chunk.SetFlag(ChunkFlags.HasLightData, false);
            }
        }
    }
}