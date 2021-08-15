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

        /// <summary> Checks if the specified file can be processed by this sink. </summary>
        public abstract bool Accepts(string filename, long length);
        public abstract Task Process(string filename, IProgress<double> progress);

        /// <summary> Called after all files have been processed. </summary>
        public virtual Task Finish() => Task.CompletedTask;

        public virtual void Dispose() { }

        /// <summary> Opens the input stream for the specified filename. </summary>
        protected Task<Stream> OpenFaucet(string name)
        {
            _logger.Trace("Opening input file '{0}'", name);
            return _packer.OpenInput(name);
        }
        /// <summary> Opens the output stream for the specified filename. </summary>
        /// <remarks> Only one drain should be open at any given time; otherwise this method may deadlock. </remarks>
        protected Task<Stream> OpenDrain(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
        {
            _logger.Trace("Opening output file '{0}'", name);
            return _packer.CreateOutput(name, compLevel);
        }
    }
}