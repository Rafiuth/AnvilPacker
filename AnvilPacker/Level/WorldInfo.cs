using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level
{
    public class WorldInfo
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public string Path { get; }

        /// <summary> Returns a serializer capable of handling the specified anvil tag. </summary>
        public IChunkSerializer GetSerializer(CompoundTag tag)
        {
            int version = tag.GetInt("DataVersion");
            for (int i = _serializers.Length - 1; i >= 0; i--) {
                var (minVer, maxVer, serializer) = _serializers[i];
                if (version >= minVer) {
                    if (version > maxVer) {
                        _logger.Warn($"Chunk serializer for v{version} not available, using latest: v{minVer} to v{maxVer}");
                    }
                    return _serializers[i].Serializer;
                }
            }
            throw new FormatException($"Bad chunk data version {version}");
        }

        private static readonly (int MinVersion, int MaxVersion, IChunkSerializer Serializer)[] _serializers = {
            (2566, 2586, new Versions.v1_16.ChunkSerializer()),
            (0,    1343, new Versions.v1_8.ChunkSerializer())
        };
    }
}
