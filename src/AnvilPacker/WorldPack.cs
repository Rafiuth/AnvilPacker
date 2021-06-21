using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker
{
    public abstract class WorldPackProcessor : IDisposable
    {
        public const string REGION_EXT = "apr"; //Anvil Pack Region

        protected readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        protected WorldInfo _world;
        protected PackMetadata _meta;

        protected PackerTaskProgress _regionProgress = new() { Name = "Regions" };
        protected PackerTaskProgress _opaqueProgress = new() { Name = "Passtrough" };

        public virtual IEnumerable<PackerTaskProgress> TaskProgresses => new[] {
            _regionProgress, _opaqueProgress
        };

        public abstract Task Run(int maxThreads);

        protected void CopyStream(Stream src, Stream dst, long length, PackerTaskProgress? progress = null)
        {
            if (length == 0) {
                progress?.Inc(1.0); //avoid div by 0
                return;
            }

            var buf = ArrayPool<byte>.Shared.Rent(32768);
            try {
                while (true) {
                    int bytesRead = src.Read(buf);
                    if (bytesRead <= 0) break;

                    dst.Write(buf, 0, bytesRead);
                    progress?.Inc(bytesRead / (double)length);
                }
            } finally {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }

        protected void LogStatus(string msg, string? filename = null)
        {
            if (filename != null && Path.IsPathFullyQualified(filename)) {
                filename = Path.GetRelativePath(_world.RootPath, filename);
            }
            _logger.Info(msg, filename);
        }

        public abstract void Dispose();
    }
    
    public class PackMetadata
    {
        public string Version = null!;  //version + commit of the encoder
        public int DataVersion = 0;     //version number of this object
        public DateTime Timestamp;      //time the file was encoded - probably useless
        public List<ReversibleTransform> Transforms = null!; //transforms for the decoder to apply
    }
    public class PackerTaskProgress
    {
        public string Name = "";
        public double ProcessedItems = 0;
        public int TotalItems = 0;

        internal void AddItem()
        {
            TotalItems++;
        }
        internal void Inc(double delta)
        {
            Utils.InterlockedAdd(ref ProcessedItems, delta);
        }
        internal Progress<double> CreateProgressListener()
        {
            double prevProgress = 0;
            return new Progress<double>(currProgress => {
                Inc(currProgress - prevProgress);
                prevProgress = currProgress;
            });
        }
    }
}