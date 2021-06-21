#nullable enable

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
        ArchiveEntry? FindEntry(string name);

        Stream OpenEntry(ArchiveEntry entry);
    }
    public interface IArchiveWriter : IDisposable
    {
        /// <summary> Whether this archive supports writting multiple entries at the same time. </summary>
        bool SupportsSimultaneousWrites => false;

        Stream CreateEntry(string name, CompressionLevel compLevel = CompressionLevel.Optimal);
    }

    public abstract class ArchiveEntry
    {
        public abstract string Name { get; }
        public abstract long Size { get; }
        public abstract long CompressedSize { get; }
        public abstract DateTimeOffset Timestamp { get; }
    }
}