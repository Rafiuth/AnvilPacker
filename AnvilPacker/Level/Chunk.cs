#nullable enable

using System;
using System.Collections;
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
        public readonly ChunkSectionBase?[] Sections = new ChunkSectionBase[16];
        public long LastUpdate;
        public long InhabitedTime;
        public bool IsTerrainPopulated;
        public bool IsLightPopulated;

        public List<ScheduledTick> TileTicks = new();
        /// <summary> Data that the encoder doesn't know how to process. Contents are left unmodified. </summary>
        public CompoundTag? OpaqueData;

        /// <summary> Section Y extents, in chunk coordinates (blockPos / 16). Max is exclusive. </summary>
        public readonly int MinSectionY, MaxSectionY;

        public ChunkBase(int x, int z)
        {
            X = x;
            Z = z;
            MinSectionY = 0;
            MaxSectionY = 16;
        }

        /// <param name="y">Section Y coord (blockY >> 4)</param>
        public ChunkSectionBase? GetSection(int y)
        {
            return (uint)y < 16u ? Sections[y] : null;
        }
        /// <param name="y">Section Y coord (blockY >> 4)</param>
        /// <remarks><see cref="IndexOutOfRangeException"/> is thrown if y is outside [0..15]</remarks>
        public void SetSection(int y, ChunkSectionBase? section)
        {
            Sections[y] = section;
        }

        public BlockState GetBlock(int x, int y, int z)
        {
            var sect = GetSection(y >> 4);
            return sect != null ? sect.GetBlock(x, y & 15, z) : BlockState.Air;
        }
        public void SetBlock(int x, int y, int z, BlockState block)
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
        protected abstract ChunkSectionBase CreateSection();
    }
    public abstract class ChunkSectionBase
    {
        public NibbleArray? SkyLight { get; set; }
        public NibbleArray? BlockLight { get; set; }

        public abstract BlockState GetBlock(int x, int y, int z);
        public abstract void SetBlock(int x, int y, int z, BlockState block);

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
        
        public static int GetIndex(int x, int y, int z) => y << 8 | z << 4 | x;
    }

    public class NibbleArray : IEnumerable<int>
    {
        public readonly byte[] Data;
        public readonly int Length;

        public int this[int index]
        {
            get {
                //byte b = arr[idx / 2];
                //return i % 2 == 0 ? b & 15 : b >> 4;
                byte b = Data[index >> 1];
                int s = (index & 1) * 4;
                return (b >> s) & 15;
            }
            set {
                ref byte b = ref Data[index >> 1];
                int s = (index & 1) * 4;
                int m = 0xF0 >> s;
                b = (byte)((b & m) | (value & 15) << s);
            }
        }

        public NibbleArray(byte[] data)
        {
            Data = data;
            Length = data.Length * 2;
        }
        public NibbleArray(int length)
        {
            Data = new byte[(length + 1) / 2];
            Length = length;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < Length; i++) {
                yield return this[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct ScheduledTick
    {
        public int X, Y, Z;
        public int Delay;
        public int Priority;
        public Block Type;
    }
}
