using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Archives
{
    public class FileSystemArchiveReader : IArchiveReader
    {
        private string _root;

        public int EntryCount => -1;

        public FileSystemArchiveReader(string rootPath)
        {
            Ensure.That(Directory.Exists(rootPath), "rootPath must be a valid directory");
            _root = rootPath;
        }

        public IEnumerable<ArchiveEntry> ReadEntries()
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) {
                yield return new Entry(_root, file);
            }
        }
        public ArchiveEntry FindEntry(string name)
        {
            var path = Path.Combine(_root, name);
            if (!File.Exists(path)) {
                return null;
            }
            return new Entry(_root, path);
        }

        public Stream OpenEntry(ArchiveEntry entry)
        {
            return File.OpenRead(((Entry)entry)._fullPath);
        }

        public void Dispose()
        {
        }

        private class Entry : ArchiveEntry
        {
            public readonly string _fullPath;

            public override string Name { get; }
            public override long Size { get; }
            public override long CompressedSize => Size;
            public override DateTimeOffset Timestamp { get; }

            public Entry(string rootPath, string fullPath)
            {
                _fullPath = fullPath;
                Name = Path.GetRelativePath(rootPath, fullPath);

                var info = new FileInfo(fullPath);
                Size = info.Length;
                Timestamp = info.LastWriteTime;
            }
        }
    }

    public class FileSystemArchiveWriter : IArchiveWriter
    {
        private string _root;

        public bool SupportsSimultaneousWrites => true;

        public FileSystemArchiveWriter(string rootPath)
        {
            Ensure.That(!Directory.Exists(rootPath), "rootPath already exists");
            _root = rootPath;
        }

        public Stream CreateEntry(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return File.Create(path);
        }

        public void Dispose()
        {
        }
    }
}