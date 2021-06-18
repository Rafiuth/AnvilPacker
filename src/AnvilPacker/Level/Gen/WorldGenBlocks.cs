namespace AnvilPacker.Level.Gen
{
    /// <summary> Holds common blocks used in worldgen, for a specific palette. </summary>
    public class WorldGenBlocks
    {
        public BlockId Air;
        public BlockId Stone;
        public BlockId Dirt;
        public BlockId Grass;
        public BlockId Water;
        public BlockId Lava;
        public BlockId Sand;

        private BlockPalette _palette;
        private WorldGenVersion _version;

        public void Update(Chunk chunk, WorldGenSettings settings)
        {
            if (_palette == chunk.Palette && _version == settings.Version) {
                return;
            }
            _palette = chunk.Palette;
            _version = settings.Version;

            BlockId Get(string name, int legacyId, int legacyMeta = 0)
            {
                BlockState state;
                if (_version <= WorldGenVersion.v1_8) {
                    state = BlockRegistry.GetLegacyState(legacyId << 4 | legacyMeta);
                } else {
                    state = BlockRegistry.ParseState(name);
                }
                return _palette.GetOrAddId(state);
            }

            Air     = Get("air",            0);
            Stone   = Get("stone",          1);
            Dirt    = Get("dirt",           3);
            Grass   = Get("grass_block",    2);
            Water   = Get("water",          9);
            Lava    = Get("lava",           11);
            Sand    = Get("sand",           12);
        }
    }
}