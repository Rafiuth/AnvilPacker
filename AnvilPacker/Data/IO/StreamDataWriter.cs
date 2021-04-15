using System;
using System.IO;

namespace AnvilPacker.Data
{
    /// <summary> Big-endian binary writer </summary>
    public class StreamDataWriter : DataWriter
    {
        private readonly Stream _s;
        private readonly bool _leaveOpen;

        public Stream BaseStream => _s;

        public override long Position
        {
            get => _s.Position;
            set => _s.Position = value;
        }
        public long Length
        {
            get => _s.Length;
        }

        public StreamDataWriter(Stream s, bool leaveOpen = false)
        {
            _s = s;
            _leaveOpen = leaveOpen;
        }

        public override void WriteBytes(ReadOnlySpan<byte> buf) => _s.Write(buf);

        public override void Dispose()
        {
            if (!_leaveOpen) {
                _s.Dispose();
            }
        }
    }
}
