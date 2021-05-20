using System;
using System.Linq;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    /// <summary> Calculates metadata for decoded chunks. </summary>
    public class Primer
    {
        public RegionBuffer Region { get; init; }
        public bool CalcHeightmaps { get; init; } = true;

        public void Prime(IProgress<double> progress = null)
        {
            if (CalcHeightmaps) {
                PrimeHeightmaps();
            }
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