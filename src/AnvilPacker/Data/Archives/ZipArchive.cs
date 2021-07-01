using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AnvilPacker.Data.Archives
{
    public class ZipArchiveReader : IArchiveReader
    {
        private ZipArchive _zip;

        public int EntryCount => _zip.Entries.Count;

        public ZipArchiveReader(string path)
        {
            _zip = ZipFile.OpenRead(path);
        }

        public IEnumerable<ArchiveEntry> ReadEntries()
        {
            return _zip.Entries.Select(e => new Entry(e));
        }
        public ArchiveEntry FindEntry(string name)
        {
            var entry = _zip.GetEntry(name);
            return entry == null ? null : new Entry(entry);
        }

        public Stream OpenEntry(ArchiveEntry entry)
        {
            lock (_zip) {
                var stream = ((Entry)entry)._handle.Open();
                return new SynchedStream(stream, _zip);
            }
        }

        public void Dispose()
        {
            _zip.Dispose();
        }

        private class Entry : ArchiveEntry
        {
            public readonly ZipArchiveEntry _handle;

            public override string Name => _handle.FullName;
            public override long Size => _handle.Length;
            public override long CompressedSize => _handle.CompressedLength;
            public override DateTimeOffset Timestamp => _handle.LastWriteTime;

            public Entry(ZipArchiveEntry entry)
            {
                _handle = entry;
            }
        }
    }

    public class ZipArchiveWriter : IArchiveWriter
    {
        private ZipArchive _zip;

        public bool SupportsSimultaneousWrites => false;

        public ZipArchiveWriter(string path)
        {
            _zip = ZipFile.Open(path, ZipArchiveMode.Create);
        }

        public Stream CreateEntry(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
        {
            var entry = _zip.CreateEntry(name, compLevel);
            return entry.Open();
        }

        public void Dispose()
        {
            _zip.Dispose();
        }
    }
}