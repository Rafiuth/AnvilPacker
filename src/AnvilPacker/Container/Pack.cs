#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AnvilPacker.Container.Sinks;
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
        public const string BLOB_BASE_DIR = "anvilpacker/blobs/";
        public const string METADATA_PATH = "anvilpacker/metadata.json";
        public const string ENC_REGION_EXT = ".apr"; //Anvil Pack Region

        protected readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        protected static readonly JsonSerializer _metaJsonSerializer = CreateMetaJsonSerializer();

        internal readonly IArchiveReader _inArchive;
        internal readonly IArchiveWriter _outArchive;
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
            var sinks = CreateSinks();
            var copyBuf = new byte[16384];

            while (await _pendingFiles.OutputAvailableAsync()) {
                var file = await _pendingFiles.ReceiveAsync();
                
                if (!await Process(sinks, file)) {
                    _logger.Info("Copying file '{0}'", file.Name);
                    await CopyToOutput(file.Name, copyBuf);
                }
            }

            foreach (var sink in sinks) {
                sink.Finish();
                sink.Dispose();
            }
        }

        private async Task<bool> Process(FileSink[] sinks, ArchiveEntry file)
        {
            foreach (var sink in sinks) {
                if (sink.Accepts(file.Name, file.Size)) {
                    try {
                        await sink.Process(file.Name, GetProgressListener(sink));
                        return true;
                    } catch (Exception ex) {
                        _logger.Error(ex, "Failed to process file '{0}', copying as is...", file.Name);
                    }
                    break;
                }
            }
            return false;
        }
        private async Task CopyToOutput(string filename, byte[] buf)
        {
            using var ins = _inArchive.Open(filename);
            using var outs = _outArchive.Create(filename);

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

        /// <summary> 
        /// Called before processing files. 
        /// This method should write/read metadata, and init <see cref="_world"/>. 
        /// </summary>
        protected virtual Task Begin() => Task.CompletedTask;
        protected virtual Task End() => Task.CompletedTask;
        
        protected abstract FileSink[] CreateSinks();

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
        internal IProgress<double> CreateProgressListener()
        {
            double prevProgress = 0;
            return new Progress<double>(currProgress => {
                Inc(currProgress - prevProgress);
                prevProgress = currProgress;
            });
        }
    }
}