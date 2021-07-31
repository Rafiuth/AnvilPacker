#nullable enable

using System;
using System.Threading.Tasks;
using AnvilPacker.Util;

namespace AnvilPacker.Container.Sinks
{
    /// <summary> Sink that ignores AnvilPacker's data files. </summary>
    public class MetadataDisposerSink : FileSink
    {
        public MetadataDisposerSink(PackProcessor packer)
            : base(packer)
        {
        }

        public override bool Accepts(string filename, long length)
            => Utils.IsSubPath(PackProcessor.BASE_DATA_DIR, filename);

        public override Task Process(string filename, IProgress<double> progress)
            => Task.CompletedTask;
    }
}