#nullable enable

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public unsafe class FileDataReader : DataReader
    {
        private const int BASE_BUF_SIZE = 128;
        private FileStream _fs;
        private MemoryMappedFile? _mappedFile;

        public FileDataReader(string path)
            : base(CreateBaseStream(path, out var fs, out var file), false, BASE_BUF_SIZE)
        {
            _fs = fs;
            _mappedFile = file;
        }

        private static Stream CreateBaseStream(string path, out FileStream fs, out MemoryMappedFile? mappedFile)
        {
            fs = File.OpenRead(path);
            if (fs.Length == 0) {
                mappedFile = null; //can't map an empty file, weird.
                return fs;
            }
            mappedFile = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
            //Passing 0 as the view size will fallback to the file size on disk. (aligned to page size/4KBs?)
            return mappedFile.CreateViewStream(0, fs.Length, MemoryMappedFileAccess.Read);
        }
        
        /// <summary> Creates a new view in the specified portion this file. </summary>
        public DataReader Fork(long offset, long length)
        {
            return new DataReader(ForkStream(offset, length), false, BASE_BUF_SIZE);
        }
        public Stream ForkStream(long offset, long length)
        {
            if (_mappedFile == null) {
                throw new ArgumentException("Cannot fork empty file");
            }
            return _mappedFile.CreateViewStream(offset, length, MemoryMappedFileAccess.Read);
        }

        public override void Dispose()
        {
            _mappedFile?.Dispose();
            _fs?.Dispose();
        }
    }
}