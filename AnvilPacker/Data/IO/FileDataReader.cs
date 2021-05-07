using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public unsafe class FileDataReader : DataReader
    {
        private const int BASE_BUF_SIZE = 128;
        private MemoryMappedFile _file;

        public FileDataReader(string path)
            : base(CreateBaseStream(path, out var file), false, BASE_BUF_SIZE)
        {
            _file = file;
        }

        private static Stream CreateBaseStream(string path, out MemoryMappedFile file)
        {
            //CreateViewStream() will randomly throw UnauthorizedAccessException if we dont set
            //access to ReadWrite.
            file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
            return file.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        }
        
        /// <summary> Creates a new view in the specified portion this file. </summary>
        public DataReader Fork(long offset, long length)
        {
            return new DataReader(ForkStream(offset, length), false, BASE_BUF_SIZE);
        }
        public MemoryMappedViewStream ForkStream(long offset, long length)
        {
            return _file.CreateViewStream(offset, length);
        }

        public override void Dispose()
        {
            _file.Dispose();
        }
    }
}