using System;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    /// <summary> Represents a 16x16x16 block region. </summary>
    /// <remarks>
    /// Notes: <br/>
    /// - No Get() or Set() method check coordinates, unless documented otherwise.
    /// </remarks>
    public class ChunkSection
    {
        public readonly BlockId[] Blocks;
        /// <summary> 
        /// A reference copy of the chunk palette. 
        /// It should always be the same as <see cref="Chunk.Palette" />.
        /// </summary>
        public BlockPalette Palette;
        public NibbleArray? SkyLight;
        public NibbleArray? BlockLight;

        public readonly Chunk Chunk;
        public readonly int X, Y, Z;

        public ChunkSection(Chunk chunk, int y)
        {
            Chunk = chunk;
            (X, Y, Z) = (chunk.X, y, chunk.Z);
            Blocks = new BlockId[16 * 16 * 16];
            Palette = chunk.Palette;
        }

        public BlockState GetBlock(int x, int y, int z)
        {
            var id = Blocks[GetIndex(x, y, z)];
            return Palette.GetState(id);
        }
        public BlockId GetBlockId(int x, int y, int z)
        {
            return Blocks[GetIndex(x, y, z)];
        }
        public void SetBlock(int x, int y, int z, BlockState block)
        {
            var id = Palette.GetOrAddId(block);
            Blocks[GetIndex(x, y, z)] = id;
        }
        public void SetBlockId(int x, int y, int z, BlockId id)
        {
            Blocks[GetIndex(x, y, z)] = id;
        }

        public int GetSkyLight(int x, int y, int z)
        {
            if (SkyLight != null) {
                return SkyLight[GetIndex(x, y, z)];
            }
            return 0;
        }
        public void SetSkyLight(int x, int y, int z, int value)
        {
            if (SkyLight != null) {
                SkyLight[GetIndex(x, y, z)] = value;
            }
        }
        
        public int GetBlockLight(int x, int y, int z)
        {
            if (BlockLight != null) {
                return BlockLight[GetIndex(x, y, z)];
            }
            return 0;
        }
        public void SetBlockLight(int x, int y, int z, int value)
        {
            if (BlockLight != null) {
                BlockLight[GetIndex(x, y, z)] = value;
            }
        }

        /// <summary> Checks if the specified coords are inside [0..15]. </summary>
        public static bool IsCoordInside(int x, int y, int z) => (uint)(x | y | z) < 16;
        /// <summary> Returns the block index for the specified coord, i.e.: <c>y*256 + z*16 + x</c> </summary>
        public static int GetIndex(int x, int y, int z) => y << 8 | z << 4 | x;

        public NibbleArray? GetLightData(LightLayer layer)
        {
            return layer switch {
                LightLayer.Sky      => SkyLight,
                LightLayer.Block    => BlockLight,
                _ => throw new InvalidOperationException()
            };
        }
        public NibbleArray GetOrCreateLightData(LightLayer layer)
        {
            return layer switch {
                LightLayer.Sky      => SkyLight ??= new(4096),
                LightLayer.Block    => BlockLight ??= new(4096),
                _ => throw new InvalidOperationException()
            };
        }
    }
}
