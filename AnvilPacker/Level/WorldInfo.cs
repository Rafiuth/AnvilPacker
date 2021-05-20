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
            string[] paths = { 
                "region/", 
                "DIM-1/region/", 
                "DIM1/region/"
            };
            foreach (var path in paths) {
                var fullPath = Path.Combine(RootPath, path);
                if (Directory.Exists(fullPath)) {
                    yield return fullPath;
                }
            }
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
            foreach (var (minVer, maxVer, serializer) in IChunkSerializer.KnownSerializers) {
                if (dataVersion >= minVer) {
                    if (dataVersion > maxVer) {
                        _logger.Warn($"Chunk serializer for data version '{dataVersion}' not available, using latest available: {minVer}-{maxVer}");
                    }
                    return serializer;
                }
            }
            throw new InvalidOperationException($"Chunk version '{dataVersion}' not supported.");
        }
    }
}
