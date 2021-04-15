using System;
using System.IO;

namespace AnvilPacker.Data
{
    /// <summary> Implementation of <see cref="DataReader"/> backed by a <see cref="Stream"/>, with no buffering. </summary>
    public class StreamDataReader : DataReader
    {
        private readonly Stream _s;
        private readonly bool _leaveOpen;

        /// <remarks> Note that since this reader is buffered, the base stream position may not be accurate. </remarks>
        public Stream BaseStream => _s;

        public override long Position
        {
            get => _s.Position;
            set => _s.Position = value;
        }

        public StreamDataReader(Stream s, bool leaveOpen = false)
        {
            _s = s;
            _leaveOpen = leaveOpen;
        }

        public override void ReadBytes(Span<byte> dest)
        {
            while (dest.Length > 0) {
                int count = _s.Read(dest);
                if (count <= 0) {
                    throw new EndOfStreamException();
                }
                dest = dest[count..];
            }
        }

        public override void Dispose()
        {
            if (!_leaveOpen) {
                _s.Dispose();
            }
        }
    }
}
