#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NLog;

namespace AnvilPacker.Container.Sinks
{
    public abstract class FileSink : IDisposable
    {
        protected readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        protected readonly PackProcessor _packer;
        protected bool IsEncoding => _packer is WorldPacker;

        protected FileSink(PackProcessor packer)
        {
            _packer = packer;
        }

        public abstract bool Accepts(string filename, long length);
        public abstract Task Process(string filename, IProgress<double> progress);

        /// <summary> Called after all files have been processed. </summary>
        public virtual void Finish() { }

        public virtual void Dispose() { }

        protected Stream OpenFaucet(string name)
        {
            _logger.Trace("Opening input file '{0}'", name);
            return _packer._inArchive.Open(name);
        }
        protected Stream OpenDrain(string name, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            _logger.Trace("Opening output file '{0}'", name);
            return _packer._outArchive.Create(name, compressionLevel);
        }
    }
}