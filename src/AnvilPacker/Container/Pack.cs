using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AnvilPacker.Container.Sinks;
using AnvilPacker.Data;
using AnvilPacker.Data.Archives;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;
using NLog;

namespace AnvilPacker.Container
{
    public abstract class PackProcessor : IDisposable
    {
        public const string BASE_DATA_DIR = "anvilpacker/";
        public const string BASE_BLOB_DIR = "anvilpacker/blobs/";
        public const string METADATA_PATH = "anvilpacker/metadata.json";
        public const string ENC_REGION_EXT = ".apr"; //Anvil Pack Region

        protected readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        protected static readonly JsonSerializer _metaJsonSerializer = CreateMetaJsonSerializer();

        protected readonly IArchiveReader _inArchive;
        protected readonly IArchiveWriter _outArchive;
        private readonly AsyncAutoResetEvent _outEntryAvailEvent = new(true);

        internal WorldInfo _world = null!;
        internal PackMetadata _meta = null!;

        private readonly BufferBlock<ArchiveEntry> _pendingFiles = new();
        private int _nextBlobId = 0;

        protected PackerTaskProgress _regionProgress = new() { Name = "Regions" };
        protected PackerTaskProgress _opaqueProgress = new() { Name = "Passthrough" };

        public virtual IEnumerable<PackerTaskProgress> TaskProgresses => new[] {
            _regionProgress, _opaqueProgress
        };

        public PackProcessor(string inPath, string outPath)
        {
            _inArchive = DataArchive.Open(inPath);
            _outArchive = DataArchive.Create(outPath);
        }

        public async Task Run(int maxThreads)
        {
            await Begin();
            
            var tasks = CreateWorkerTasks(maxThreads);

            _logger.Info("Discovering input files...");

            foreach (var file in _inArchive.ReadEntries()) {
                _pendingFiles.Post(file);
                AddTaskFile(file.Name);
            }
            _pendingFiles.Complete();

            _logger.Info("File discovery done, waiting the process to finish...");

            await Task.WhenAll(tasks);

            await End();
        }

        private Task[] CreateWorkerTasks(int count)
        {
            var tasks = new Task[count];
            for (int i = 0; i < count; i++) {
                tasks[i] = Task.Run(RunWorkerAsync);
            }
            return tasks;
        }

        private async Task RunWorkerAsync()
        {
            var sinks = new List<FileSink>();
            var copyBuf = new byte[16384];

            CreateSinks(sinks);

            while (await _pendingFiles.OutputAvailableAsync()) {
                while (_pendingFiles.TryReceive(out var file)) {
                    if (!await Process(sinks, file)) {
                        _logger.Info("Copying file '{0}'", file.Name);
                        await CopyToOutput(file.Name, copyBuf);
                    }
                }
            }

            foreach (var sink in sinks) {
                await sink.Finish();
                sink.Dispose();
            }
        }

        private async Task<bool> Process(List<FileSink> sinks, ArchiveEntry file)
        {
            foreach (var sink in sinks) {
                if (sink.Accepts(file.Name, file.Size)) {
                    var progress = GetProgressListener(sink);

                    try {
                        _logger.Info("Processing '{0}'", file.Name);
                        await sink.Process(file.Name, progress);
                        _logger.Trace("...processed '{0}'", file.Name);
                        return true;
                    } catch (Exception ex) {
                        _logger.Error(ex, "Failed to process file '{0}', copying as is.", file.Name);
                        return false;
                    } finally {
                        progress.Report(1.0);
                    }
                }
            }
            _opaqueProgress.ProcessedItems++;
            return false;
        }
        private async Task CopyToOutput(string filename, byte[] buf)
        {
            using var ins = await OpenInput(filename);
            using var outs = await CreateOutput(filename);

            while (true) {
                int bytesRead = await ins.ReadAsync(buf, 0, buf.Length);
                if (bytesRead <= 0) break;

                await outs.WriteAsync(buf, 0, bytesRead);
            }
        }

        private void AddTaskFile(string name)
        {
            if (name.EndsWithIgnoreCase(".mca") || name.EndsWithIgnoreCase(ENC_REGION_EXT)) {
                _regionProgress.AddItem();
            } else {
                _opaqueProgress.AddItem();
            }
        }
        private IProgress<double> GetProgressListener(FileSink sink)
        {
            var task = sink is RegionSink ? _regionProgress : _opaqueProgress;
            return task.CreateProgressListener();
        }

        internal Task<Stream> OpenInput(string name)
        {
            return Task.FromResult(_inArchive.Open(name));
        }
        internal async Task<Stream> CreateOutput(string name, CompressionLevel compLevel = CompressionLevel.Optimal)
        {
            if (_outArchive.SupportsSimultaneousWrites) {
                return _outArchive.Create(name, compLevel);
            }
            _logger.Trace("Acquiring entry lock for {0}", name);
            await _outEntryAvailEvent.WaitAsync();
            _logger.Trace("...entry lock acquired {0}", name);
            
            var stream = _outArchive.Create(name, compLevel);
            return new TrackedStream(stream, () => {
                _logger.Trace("Releasing entry lock for {0}", name);
                _outEntryAvailEvent.Set();
            }, false);
        }

        /// <summary> 
        /// Called before processing files. 
        /// This method should write/read metadata, and init <see cref="_world"/>, <see cref="_meta"/>. 
        /// </summary>
        protected virtual Task Begin() => Task.CompletedTask;
        protected virtual Task End() => Task.CompletedTask;

        protected abstract void CreateSinks(List<FileSink> sinks);

        public virtual void Dispose()
        {
            _inArchive.Dispose();
            _outArchive.Dispose();
        }

        internal int NextBlobId()
        {
            return Interlocked.Increment(ref _nextBlobId);
        }

        private static JsonSerializer CreateMetaJsonSerializer()
        {
            var ss = new JsonSerializerSettings();
            ss.Converters.Add(BlockJsonConverter.Instance);
            ss.TypeNameHandling = TypeNameHandling.Auto;
            ss.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            ss.SerializationBinder = new TypeNameSerializationBinder()
                .Map(TransformPipe.KnownTransforms);
            return JsonSerializer.CreateDefault(ss);
        }

        //TODO: find a better place for this
        /// <summary> Returns the current version of the assembly. </summary>
        public static string GetInfoVersion()
        {
            var asm = typeof(WorldPacker).Assembly;
            var attrib = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attrib?.InformationalVersion ?? asm.GetName().Version!.ToString();
        }
    }
    
    public class PackMetadata
    {
        public string Version = null!;  //version + commit of the encoder
        public int DataVersion = 0;     //version number of this object
        public DateTimeOffset Timestamp;//time the file was encoded - probably useless
        public List<ReversibleTransform> Transforms = null!; //transforms for the decoder to apply
    }
    public class PackerTaskProgress
    {
        public string Name = "";
        public double ProcessedItems;
        public int TotalItems = 0;

        internal void AddItem()
        {
            TotalItems++;
        }

        internal IProgress<double> CreateProgressListener()
        {
            return new ProgressListener() { _task = this };
        }

        class ProgressListener : IProgress<double>
        {
            internal PackerTaskProgress _task = null!;
            private double _prev = 0.0;

            public void Report(double perc)
            {
                double delta = perc - _prev;
                _prev = perc;
                Utils.InterlockedAdd(ref _task.ProcessedItems, delta);
            }
        }
    }
}