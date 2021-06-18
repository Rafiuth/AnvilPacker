using System;
using AnvilPacker.Data;

namespace AnvilPacker.Level.Gen
{
    public class BiomeProvider
    {
        public RegionBuffer Region { get; }

        // Chunk the last request was in
        private int _cx, _cz = int.MinValue;
        private Chunk _chunk;
        private Array _data;
        private int _height;
        private int _scaleBits;

        public BiomeProvider(RegionBuffer region)
        {
            Region = region;
        }

        public Biome Get(int x, int z)
        {
            var (cx, cz) = (x >> 4, z >> 4);
            if (_cx != cx || _cz != cz) {
                UpdateChunk(cx, cz);
            }

            //TODO: interpret data correctly depending on version (3D biomes, 4x4 cells, int[])
            //TODO: support 3D biomes
            int id = _data switch {
                byte[] arr  => arr[(x & 15) + (z & 15) * 16],
                int[]  arr  => arr[(x >> 2 & 3) + (z >> 2 & 3) * 4],
                null or _   => 1, //plains
            };
            return Biome.GetFromId(id);
        }

        private void UpdateChunk(int cx, int cz)
        {
            _cx = cx;
            _cz = cz;
            _chunk = Region.GetChunkAbsCoords(cx, cz);
            if (_chunk == null) {
                _data = null;
                return;
            }
            var tag = _chunk.Opaque?["Level"]?["Biomes"] as PrimitiveTag;
            _data = tag?.GetValue() as Array;
        }
    }
}