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
            return _zip.Entries.Select(e => new ArchiveEntry(e.FullName, e.Length));
        }

        public bool Exists(string name)
        {
            return _zip.GetEntry(name) != null;
        }
        public Stream Open(string name)
        {
            lock (_zip) {
                var entry = _zip.GetEntry(name) ?? throw new FileNotFoundException();
                var stream = entry.Open();
                return new SynchedStream(stream, _zip);
            }
        }

        public void Dispose()
        {
            _zip.Dispose();
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

        public Stream Create(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
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