using System;
using System.Collections.Generic;
using System.IO;
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

        public string RootPath { get; }

        public WorldInfo(string path)
        {
            RootPath = path;
        }
        
        public IEnumerable<string> GetRegionDirs()
        {
            yield return Path.Combine(RootPath, "region");
        }

        /// <summary> Returns a serializer capable of handling the specified anvil tag. </summary>
        public IChunkSerializer GetSerializer(CompoundTag tag)
        {
            int version = tag.GetInt("DataVersion", TagGetMode.Null);
            return GetSerializer(version);
        }
        public IChunkSerializer GetSerializer(Chunk chunk)
        {
            return GetSerializer(chunk.DataVersion);
        }

        public IChunkSerializer GetSerializer(int dataVersion)
        {
            foreach (var (minVer, maxVer, serializer) in _serializers) {
                if (dataVersion >= minVer) {
                    if (dataVersion > maxVer) {
                        _logger.Warn($"Chunk serializer for v{dataVersion} not available, using latest: v{minVer} to v{maxVer}");
                    }
                    return serializer;
                }
            }
            throw new InvalidOperationException(); //unreachable
        }


        private static readonly (int MinVersion, int MaxVersion, IChunkSerializer Serializer)[] _serializers = {
            (2566, 2586, new Versions.v1_16.ChunkSerializer()),
            (0,    1343, new Versions.v1_8.ChunkSerializer())
        };
    }
}
