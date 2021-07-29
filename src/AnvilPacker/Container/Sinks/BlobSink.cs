#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Encoder;
using AnvilPacker.Util;

namespace AnvilPacker.Container.Sinks
{
    public abstract class BlobSink : FileSink
    {
        protected const int MAX_BLOB_SIZE = 1024 * 1024 * 4;
        protected const int MAX_ENTRY_SIZE = 1024 * 64;

        protected DeflateHelper _deflater = new DeflateHelper(MAX_BLOB_SIZE, MAX_BLOB_SIZE);

        protected static readonly string[] FILE_EXTS = {
            ".json", ".dat", ".nbt", ".mcfunction",
            ".yml", ".yaml", ".toml",
        };

        protected BlobSink(PackProcessor packer) 
            : base(packer)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            _deflater.Dispose();
        }

        protected enum EntryType : byte
        {
            End     = 0,
            Plain   = 1, //raw data
            Gzipped = 2, //gzipped data
        }
    }
    public class BlobEncSink : BlobSink
    {
        private MemoryDataWriter _mem = new(MAX_BLOB_SIZE);
        private DataWriter? _compr;

        public BlobEncSink(PackProcessor packer)
            : base(packer)
        {
            BeginBlob();
        }

        public override bool Accepts(string filename, long length)
        {
            return length <= MAX_ENTRY_SIZE &&
                   filename.EndsWithAnyIgnoreCase(FILE_EXTS);
        }

        public override async Task Process(string filename, IProgress<double> progress)
        {
            var rawData = await ReadData(filename);
            var (type, data) = DetectType(rawData, filename);

            if (data.Length > MAX_BLOB_SIZE * 3 / 4) {
                using var outs = OpenDrain(filename);
                await outs.WriteAsync(rawData);
                return;
            }
            if (_mem.Position + data.Length > MAX_BLOB_SIZE) {
                Flush();
            }
            var stream = _compr!;
            stream.WriteByte((byte)type);
            stream.WriteNulString(filename);
            stream.WriteVarUInt(data.Length);
            stream.WriteBytes(data.Span);
        }

        private async Task<Memory<byte>> ReadData(string filename)
        {
            using var stream = OpenFaucet(filename);
            int pos = 0;
            int len = (int)stream.Length;
            var inBuf = _deflater.AllocInBuffer(len).Array!;
            while (pos < len) {
                int bytesRead = await stream.ReadAsync(inBuf, pos, len - pos);
                pos += bytesRead;
                Ensure.That(bytesRead > 0);
            }
            return inBuf.AsMemory(0, len);
        }

        private (EntryType Type, Memory<byte> Data) DetectType(Memory<byte> data, string filename)
        {
            var newData = TryDecompressGZip(data.Span);
            if (!newData.IsEmpty) {
                return (EntryType.Gzipped, newData);
            }
            return (EntryType.Plain, data);
        }
        private Memory<byte> TryDecompressGZip(Span<byte> data)
        {
            //bail out if the data doesn't match the gzip header
            if (!DeflateHelper.HasGzipHeader(data)) {
                return null;
            }
            try {
                return _deflater.Decompress(data, DeflateFlavor.Gzip);
            } catch {
                return null;
            }
        }

        private void Flush(bool reset = true)
        {
            if (_mem.Position <= 0) return;

            _compr!.WriteByte(0); //end
            _compr!.Dispose();

            var blobName = Path.Combine(PackProcessor.BLOB_BASE_DIR, _packer.NextBlobId().ToString());
            using (var stream = OpenDrain(blobName, CompressionLevel.NoCompression)) {
                stream.Write(_mem.BufferSpan);
            }
            if (reset) {
                _mem.Clear();
                BeginBlob();
            }
        }
        private void BeginBlob()
        {
            _mem.WriteByte(0); //version
            _compr = Compressors.NewBrotliEncoder(_mem, true, 6, 22);
        }

        public override void Finish()
        {
            Flush(false);
        }
    }
    public class BlobDecSink : BlobSink
    {
        public BlobDecSink(PackProcessor packer) 
            : base(packer)
        {
        }

        public override bool Accepts(string filename, long length)
            => Utils.IsSubPath(PackProcessor.BLOB_BASE_DIR, filename);

        public override Task Process(string filename, IProgress<double> progress)
        {
            using var inStream = OpenFaucet(filename);
            int version = inStream.ReadByte();
            Ensure.That(version <= 0, "Unsupported blob version");

            using var br = Compressors.NewBrotliDecoder(inStream);
            while (true) {
                var type = (EntryType)br.ReadByte();
                if (type == EntryType.End) break;

                string name = br.ReadNulString();
                int length = br.ReadVarUInt();

                var buffer = _deflater.AllocInBuffer(length);
                br.ReadBytes(buffer);

                switch (type) {
                    case EntryType.Plain: break;
                    case EntryType.Gzipped: {
                        buffer = _deflater.Compress(buffer, DeflateFlavor.Gzip);
                        break;
                    }
                    default: throw new NotSupportedException();
                }

                using var outs = OpenDrain(name);
                outs.Write(buffer);
            }
            return Task.CompletedTask;
        }
    }
}