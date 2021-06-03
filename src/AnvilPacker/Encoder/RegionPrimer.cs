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
        public TransformPipe Transforms { get;}

        public RegionPrimer(RegionBuffer region, TransformPipe transforms)
        {
            Region = region;
            Transforms = transforms;
        }

        public void Prime(IProgress<double> progress = null)
        {
            PrimeHeightmaps();

            //Transforms.Reverse(Region);
        }

        private void PrimeHeightmaps()
        {
            foreach (var type in HeightMapType.KnownTypes) {
                var computer = new HeightMapComputer(Region, type);

                foreach (var chunk in Region.Chunks.ExceptNull()) {
                    if (ShouldHeightmapExist(chunk, type)) {
                        computer.Compute(chunk);
                    }
                }
            }
        }
        private bool ShouldHeightmapExist(Chunk chunk, HeightMapType type)
        {
            if (chunk.DataVersion < DataVersions.v1_13_s5) {
                return type == HeightMapType.Legacy;
            }
            var status = chunk.Opaque["Level"]["Status"]?.Value<string>();
            bool statusComplete = status is "full" or "heightmaps" or "spawn" or "light";
            return statusComplete == type.KeepAfterWorldGen;
        }
    }
}