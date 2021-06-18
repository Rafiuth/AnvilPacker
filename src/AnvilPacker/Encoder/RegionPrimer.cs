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
        public TransformPipe Transforms { get; }
        public EstimatedBlockAttribs EstimatedBlockAttribs { get; }

        public RegionPrimer(RegionBuffer region, TransformPipe transforms, EstimatedBlockAttribs estimatedBlockAttribs)
        {
            Region = region;
            Transforms = transforms;
            EstimatedBlockAttribs = estimatedBlockAttribs;
        }

        public void Prime(IProgress<double> progress = null)
        {
            Transforms.Reverse(Region);

            PrimeHeightmaps();
        }

        private void PrimeHeightmaps()
        {
            var attribs = EstimatedBlockAttribs.HeightmapAttribs;

            foreach (var (type, isOpaque) in attribs.BlockOpacityPerType) {
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
            if (chunk.DataVersion < DataVersions.v1_13_s5) {
                return type == Heightmap.TYPE_LEGACY;
            }
            var status = chunk.Opaque["Level"]["Status"]?.Value<string>();
            bool statusComplete = status is "full" or "heightmaps" or "spawn" or "light";
            return statusComplete && !type.EndsWith("_WG");
        }
    }
}