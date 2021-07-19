using AnvilPacker.Level;
using NLog;

namespace AnvilPacker.Encoder.Transforms
{
    public abstract class TransformBase
    {
        protected readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public abstract void Apply(RegionBuffer region);
    }
}
