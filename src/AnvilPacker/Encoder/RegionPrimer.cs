using System;
using System.Linq;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    /// <summary> Computes reproducible data for decoded regions. </summary>
    public class RegionPrimer
    {
        private readonly RegionBuffer _region;
        private readonly EstimatedBlockAttribs _estimAttribs;

        public RegionPrimer(RegionBuffer region, EstimatedBlockAttribs estimAttribs)
        {
            _region = region;
            _estimAttribs = estimAttribs;
        }

        public void Prime()
        {
            PrimeHeightmaps();
            PrimeLights();
        }

        private void PrimeHeightmaps()
        {
            var attribs = _estimAttribs.HeightmapAttribs;

            foreach (var (type, isOpaque) in attribs.OpacityMap) {
                var computer = new HeightmapComputer(_region, type, isOpaque);

                foreach (var chunk in _region.ExistingChunks) {
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
            var lighter = new Lighter();
            lighter.Compute(_region, _estimAttribs.LightAttribs);
        }
    }
}