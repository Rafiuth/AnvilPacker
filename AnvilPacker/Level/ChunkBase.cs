#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public abstract class ChunkBase
    {
        public readonly int X, Z;
        public readonly IChunkSection?[] Sections = new IChunkSection[16];
        public readonly IBlockState AirBlock;

        public long LastUpdate;
        public long InhabitedTime;
        public bool IsTerrainPopulated;
        public bool IsLightPopulated;

        public List<ScheduledTick> TileTicks = new();
        /// <summary> Data that the encoder doesn't know how to process. Contents are left unmodified. </summary>
        public CompoundTag? OpaqueData;

        public ChunkBase(int x, int z, IBlockState airBlock)
        {
            X = x;
            Z = z;
            AirBlock = airBlock;
        }

        /// <param name="y">Section Y coord (blockY >> 4)</param>
        public IChunkSection? GetSection(int y)
        {
            return (uint)y < 16u ? Sections[y] : null;
        }
        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks><see cref="IndexOutOfRangeException"/> is thrown if y is outside [0..15]</remarks>
        public void SetSection(int y, IChunkSection? section)
        {
            Sections[y] = section;
        }

        public IBlockState GetBlock(int x, int y, int z)
        {
            var sect = GetSection(y >> 4);
            return sect != null ? sect.GetBlock(x, y & 15, z) : AirBlock;
        }
        public void SetBlock(int x, int y, int z, IBlockState block)
        {
            var section = GetSection(y >> 4);
            if (section == null) {
                if ((uint)y >= 256) return;

                section = CreateSection();
                SetSection(y >> 4, section);
            }
            section.SetBlock(x, y & 15, z, block);
        }

        /// <summary> Creates a new empty chunk section. </summary>
        protected abstract IChunkSection CreateSection();
    }
    public interface IChunkSection
    {
        public IBlockState GetBlock(int x, int y, int z);
        public void SetBlock(int x, int y, int z, IBlockState block);
    }

    public struct LightState
    {
        private byte _value;

        public int Sky
        {
            get => _value & 0xF;
            set => _value = (byte)((_value & 0xF0) | (value & 0x0F));
        }
        public int Block
        {
            get => _value >> 4;
            set => _value = (byte)((_value & 0x0F) | ((value & 0xF) << 4));
        }

        public LightState(int sky, int block)
        {
            _value = (byte)((block & 15) << 4 | (sky & 15));
        }
        /// <summary> Constructs the light state from a value packed as <c>sky | block &lt;&lt; 4</c></summary>
        public LightState(int value)
        {
            _value = (byte)value;
        }

        public static void RepackLights(byte[]? sky, byte[]? blk, LightState[] dest)
        {
            Debug.Assert(dest.Length % 2 == 0);
            Debug.Assert(sky == null || sky.Length * 2 == dest.Length);
            Debug.Assert(blk == null || blk.Length * 2 == dest.Length);

            for (int i = 0; i < dest.Length; i += 2) {
                byte s = sky == null ? 0 : sky[i >> 1];
                byte b = blk == null ? 0 : blk[i >> 1];

                dest[i + 0] = new LightState(s & 15, b & 15);
                dest[i + 1] = new LightState(s >> 4, b >> 4);
            }
        }

        public override string ToString() => $"Light(S={Sky} B={Block})";
    }

    public struct ScheduledTick
    {
        /// <summary> Tile position relative to chunk </summary>
        public int X, Y, Z;
        public int Delay;
        public int Priority;
        public IBlock Type;
    }
}
