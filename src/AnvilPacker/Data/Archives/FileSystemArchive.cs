using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
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
            if (!Directory.Exists(rootPath)) {
                throw new ArgumentException($"Directory '{rootPath}' does not exist.");
            }
            _root = rootPath;
        }

        public IEnumerable<ArchiveEntry> ReadEntries()
        {
            var opts = new EnumerationOptions() {
                RecurseSubdirectories = true,
            };
            var enumerable = new FileSystemEnumerable<ArchiveEntry>(_root, Map, opts) {
                ShouldIncludePredicate = Filter
            };
            return enumerable;

            static ArchiveEntry Map(ref FileSystemEntry e)
            {
                var root = e.RootDirectory;
                var dir = e.Directory.Slice(root.Length);
                //remove leading /
                if (!Path.EndsInDirectorySeparator(root) && dir.Length > 0) {
                    dir = dir.Slice(1);
                }
                return new ArchiveEntry(Path.Join(dir, e.FileName), e.Length);
            }
            static bool Filter(ref FileSystemEntry e)
            {
                return !e.IsDirectory;
            }
        }

        public bool Exists(string name)
        {
            return File.Exists(Path.Combine(_root, name));
        }
        public Stream Open(string name)
        {
            return File.OpenRead(Path.Combine(_root, name));
        }

        public void Dispose()
        {
        }
    }

    public class FileSystemArchiveWriter : IArchiveWriter
    {
        private string _root;

        public bool SupportsSimultaneousWrites => true;

        public FileSystemArchiveWriter(string rootPath)
        {
            if (Directory.Exists(rootPath)) {
                throw new ArgumentException($"Directory '{rootPath}' already exists.");
            }
            _root = rootPath;
        }

        public Stream Create(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
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