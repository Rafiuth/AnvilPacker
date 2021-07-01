using System;
using System.IO;
using AnvilPacker.Util;

namespace AnvilPacker.Data.Archives
{
    public static class DataArchive
    {
        public static IArchiveWriter Create(string path, ArchiveType type = ArchiveType.Auto)
        {
            if (type == ArchiveType.Auto) {
                type = PickType(path);
            }
            return type switch {
                ArchiveType.Zip         => new ZipArchiveWriter(path),
                ArchiveType.FileSystem  => new FileSystemArchiveWriter(path),
                _ => throw new ArgumentException("Unknown archive type")
            };
        }

        public static IArchiveReader Open(string path)
        {
            return DetectType(path) switch {
                ArchiveType.Zip         => new ZipArchiveReader(path),
                ArchiveType.FileSystem  => new FileSystemArchiveReader(path),
                _ => throw new ArgumentException("Unknown archive type")
            };
        }

        private static ArchiveType PickType(string path)
        {
            if (Path.HasExtension(path)) {
                return ArchiveType.Zip;
            }
            return ArchiveType.FileSystem;
        }
        private static ArchiveType DetectType(string path)
        {
            if (File.Exists(path)) {
                return ArchiveType.Zip;
            }
            if (Directory.Exists(path)) {
                return ArchiveType.FileSystem;
            }
            throw new NotSupportedException($"Unsupported archive type in path '{path}'");
        }
    }
    public enum ArchiveType
    {
        /// <summary> Select the type based on the path extension. </summary>
        Auto,
        /// <summary> Not an archive - backed by the file system </summary>
        FileSystem,
        /// <summary> Zip Archive </summary>
        Zip
    }
}