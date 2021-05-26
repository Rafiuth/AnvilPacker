using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using AnvilPacker.Util;

namespace AnvilPacker.Data
{
    public class MemoryDataWriter : DataWriter
    {
        public byte[] Buffer
        {
            get {
                Flush();
                var mem = (MemoryStream)BaseStream;
                return mem.GetBuffer();
            }
        }
        public Span<byte> BufferSpan
        {
            get {
                Flush();
                var mem = (MemoryStream)BaseStream;
                return mem.GetBuffer().AsSpan(0, checked((int)mem.Position));
            }
        }
        public Memory<byte> BufferMem
        {
            get {
                Flush();
                var mem = (MemoryStream)BaseStream;
                return mem.GetBuffer().AsMemory(0, checked((int)mem.Position));
            }
        }

        public MemoryDataWriter(int initialCapacity = 4096) 
            : base(new MemoryStream(initialCapacity), true, 64)
        {
        }

        public void Clear()
        {
            Position = 0;
            Length = 0;
        }
    }
}