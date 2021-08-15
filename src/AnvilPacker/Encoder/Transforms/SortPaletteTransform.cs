using System;
using System.Linq;
using AnvilPacker.Level;

namespace AnvilPacker.Encoder.Transforms
{
    /// <summary> Sorts region palettes based on a specific method. </summary>
    public class SortPaletteTransform : TransformBase
    {
        public enum SortMode
        {
            Frequency,
            //TODO: sort by the first seen order
        }

        public SortMode Mode = SortMode.Frequency;

        public override void Apply(RegionBuffer region)
        {
            var (newPalette, newIndices) = SortPalette(region);

            //reindex blocks
            foreach (var section in ChunkIterator.GetSections(region)) {
                section.Chunk.Palette = newPalette;
                section.Palette = newPalette;

                foreach (ref var block in section.Blocks.AsSpan()) {
                    block = newIndices[block];
                }
            }
            region.Palette = newPalette;
        }

        private (BlockPalette NewPalette, BlockId[] NewIndices) SortPalette(RegionBuffer region)
        {
            var palette = region.Palette;
            var newPalette = new BlockPalette(palette.Count);
            var newIndices = new BlockId[palette.Count];
            var states = palette.ToArray();

            switch (Mode) {
                case SortMode.Frequency: SortByFreq(region, states); break;
                default: throw new NotImplementedException();
            }

            foreach (var state in states) {
                if (state != null) {
                    var oldId = palette.GetId(state);
                    var newId = newPalette.Add(state);
                    newIndices[oldId] = newId;
                }
            }
            return (newPalette, newIndices);
        }
        private void SortByFreq(RegionBuffer region, BlockState?[] states)
        {
            int[] freq = new int[states.Length];

            foreach (var section in ChunkIterator.GetSections(region)) {
                foreach (var block in section.Blocks) {
                    //decrementing will remove the need of a
                    //custom comparer to sort in descending order.
                    freq[block]--;
                }
            }
            Array.Sort(freq, states);

            //remove unused blocks
            for (int i = 0; i < freq.Length; i++) {
                if (freq[i] == 0) {
                    states[i] = null;
                }
            }
        }
    }
}
