#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker
{
    //TODO: Should enc and dec be in separate classes?
    //https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library
    public partial class WorldPacker : IDisposable
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private WorldInfo _world;
        private ZipArchive _zip;
        private TransformPipe _transforms = TransformPipe.Empty;
        private int _maxThreads;
        private string _zipPath;

        private PackMetadata _meta = new() {
            Version = "a0.1", //TODO: set this on build
            DataVersion = 1,
            Transforms = new List<ReversibleTransform>(),
            Timestamp = DateTime.UtcNow
        };

        private PackerTaskProgress _regionProgress = new() { Name = "Regions" };
        private PackerTaskProgress _opaqueProgress = new() { Name = "Passtrough" };

        public PackerTaskProgress[] TaskProgresses => new[] {
            _regionProgress, _opaqueProgress 
        };

        public WorldPacker(string worldPath, string zipPath, int maxThreads)
        {
            _world = new WorldInfo(worldPath);
            _zipPath = zipPath;
            _maxThreads = maxThreads;
        }
        private void CopyStream(Stream src, Stream dst, long length, PackerTaskProgress? progress = null, object? readLock = null)
        {
            if (length == 0) {
                progress.Inc(1.0); //don't corrupt the progress to NaN
                return;
            }

            var buf = ArrayPool<byte>.Shared.Rent(65536);
            try {
                int bytesRead = 1;

                while (bytesRead > 0) {

                    if (readLock == null) {
                        bytesRead = src.Read(buf);
                    } else {
                        lock (readLock) {
                            bytesRead = src.Read(buf);
                        }
                    }

                    dst.Write(buf, 0, bytesRead);
                    progress?.Inc(bytesRead / (double)length);
                }
            } finally {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }

        private void LogStatus(string msg, string? filename = null)
        {
            if (filename != null && Path.IsPathFullyQualified(filename)) {
                filename = Path.GetRelativePath(_world.RootPath, filename);
            }
            _logger.Info(msg, filename);
        }

        public void Dispose()
        {
            _zip?.Dispose();
        }
    }

    /// <summary> Custom metadata for a packed world. </summary>
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