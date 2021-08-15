using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AnvilPacker.Data.Archives
{
    public interface IArchiveReader : IDisposable
    {
        /// <summary> Number of entries in the archive. <c>-1</c> if unknown. </summary>
        int EntryCount { get; }

        IEnumerable<ArchiveEntry> ReadEntries();

        bool Exists(string name);
        
        /// <exception cref="FileNotFoundException" />
        Stream Open(string name);
    }
    public interface IArchiveWriter : IDisposable
    {
        /// <summary> Whether this archive supports writting multiple entries at the same time. </summary>
        bool SupportsSimultaneousWrites => false;

        Stream Create(string name, CompressionLevel compLevel = CompressionLevel.Optimal);
    }

    public struct ArchiveEntry
    {
        public string Name { get; }
        public long Size { get; }

        public ArchiveEntry(string name, long size)
        {
            Name = name;
            Size = size;
        }

        public override string ToString()
        {
            return $"{Name} ({Size / 1024.0:0.000}KB";
        }
    }
}