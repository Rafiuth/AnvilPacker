using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level
{
    public class RegistryLoader
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private static bool _loaded = false;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            
            var sw = Stopwatch.StartNew();

            LoadBlocks();
            LoadLegacyBlocks();

            BlockRegistry.Air = BlockRegistry.GetBlock("air").DefaultState;

            sw.Stop();
            _logger.Debug($"Block registry loaded in {sw.ElapsedMilliseconds}ms");
        }

        private static void LoadBlocks()
        {
            using var json = LoadJson("blocks.json");
            var arr = json.RootElement.GetProperty("blocks");

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in arr.EnumerateArray()) {
                var name = ResourceName.Parse(jb.GetString("name"));
                int minStateId = jb.GetInt("minStateId");
                int maxStateId = jb.GetInt("maxStateId");
                int defaultStateId = jb.GetInt("defaultStateId");
                var props = ParseProperties(jb.GetProperty("properties"), propCache);
                var materialName = jb.GetString("material");

                var states = new BlockState[maxStateId - minStateId + 1];
                var block = new Block() {
                    Name = name,
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName],
                    IsKnown = true
                };

                var statesProp = jb.GetProperty("states");
                var stateFlags = statesProp.GetProperty("flags");
                var stateLight = statesProp.GetProperty("light"); //packed light info, luminance << 4 | opacity
                bool stateFlagsIsArr = stateFlags.ValueKind == JsonValueKind.Array;
                bool stateLightIsArr = stateLight.ValueKind == JsonValueKind.Array;

                for (int id = minStateId; id <= maxStateId; id++) {
                    int i = id - minStateId;
                    var flags = (stateFlagsIsArr ? stateFlags[i] : stateFlags).GetInt32();
                    var light = (stateLightIsArr ? stateLight[i] : stateLight).GetInt32();

                    states[i] = new BlockState() {
                        Id = id,
                        Block = block,
                        Attributes = (BlockAttributes)flags,
                        LightOpacity = (byte)(light & 15),
                        LightEmission = (byte)(light >> 4),
                        Properties = CreatePropertyValues(props, i)
                    };
                }
                block.DefaultState = states[defaultStateId - minStateId];
                BlockRegistry.KnownBlocks.Add(block.Name, block);
            }
        }

        private static void LoadLegacyBlocks()
        {
            using var json = LoadJson("legacy_blocks.json");
            var arr = json.RootElement.GetProperty("blocks");

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in arr.EnumerateArray()) {
                var name = ResourceName.Parse(jb.GetString("name"));
                int id = jb.GetInt("id");
                int defaultStateId = jb.GetInt("defaultStateId");
                var props = ParseProperties(jb.GetProperty("properties"), propCache);
                var materialName = jb.GetString("material");
                var states = new BlockState[16];

                var block = new Block() {
                    Name = name,
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName],
                    IsKnown = true
                };

                var statesProp = jb.GetProperty("states");
                var stateFlags = statesProp.GetProperty("flags");
                var stateLight = statesProp.GetProperty("light"); //packed light info, luminance << 4 | opacity
                var stateProps = statesProp.GetProperty("states");
                bool stateFlagsIsArr = stateFlags.ValueKind == JsonValueKind.Array;
                bool stateLightIsArr = stateLight.ValueKind == JsonValueKind.Array;
                bool statePropsIsArr = stateProps.ValueKind == JsonValueKind.Array;

                for (int m = 0; m < 16; m++) {
                    int stateId = id << 4 | m;
                    var flags = (stateFlagsIsArr ? stateFlags[m] : stateFlags).GetInt();
                    var light = (stateLightIsArr ? stateLight[m] : stateLight).GetInt();
                    var rawVals = statePropsIsArr ? (m < stateProps.GetArrayLength() ? stateProps[m].GetString() : null) : stateProps.GetString();

                    if (stateId != defaultStateId && rawVals == null) {
                        continue; //avoid creating unecessary objects
                    }
                    states[m] = new BlockState() {
                        Id = stateId,
                        Block = block,
                        Attributes = (BlockAttributes)flags | BlockAttributes.Legacy,
                        LightOpacity = (byte)(light & 15),
                        LightEmission = (byte)(light >> 4)
                    };
                    if (rawVals != null) {
                        var vals = rawVals.Split(',');
                        states[m].Properties = props.Select((p, i) => (p.Name, vals[i])).ToArray();
                    }
                }
                block.DefaultState = states[defaultStateId & 15];
                //fill the states we skipped
                for (int m = 0; m < 16; m++) {
                    states[m] ??= block.DefaultState;
                }
                BlockRegistry.KnownLegacyBlocks.Add(id, block);
            }
        }

        private static (string Key, string Value)[] CreatePropertyValues(List<BlockProperty> props, int stateId)
        {
            var vals = new (string Key, string Value)[props.Count];
            int shift = 1, i = 0;
            foreach (var prop in props) {
                int valIndex = (stateId / shift) % prop.ValueCount;
                vals[i] = (prop.Name, prop.GetValue(valIndex));
                shift *= prop.ValueCount;
                i++;
            }
            return vals;
        }

        private static List<BlockProperty> ParseProperties(JsonElement jprops, HashSet<BlockProperty> cache)
        {
            var props = new List<BlockProperty>();
            foreach (var jp in jprops.EnumerateArray()) {
                var prop = ParseProperty(jp);
                if (!cache.TryGetValue(prop, out var cachedProp)) {
                    cache.Add(prop);
                    cachedProp = prop;
                }
                props.Add(cachedProp);
            }
            return props;
        }
        private static BlockProperty ParseProperty(JsonElement obj)
        {
            var name = obj.GetString("name");
            var type = obj.GetString("type");

            switch (type) {
                case "bool": {
                    return new BoolProperty(name);
                }
                case "int": {
                    int min = obj.GetInt("min");
                    int max = obj.GetInt("max");
                    return new IntProperty(name, min, max);
                }
                case "enum": {
                    var jvals = obj.GetProperty("values");
                    var values = new string[jvals.GetArrayLength()];
                    for (int i = 0; i < values.Length; i++) {
                        values[i] = jvals[i].GetString();
                    }
                    return new EnumProperty(name, values);
                }
                default: throw new NotSupportedException($"Unknown property type '{type}'");
            }
        }

        private static JsonDocument LoadJson(string filename)
        {
            var opts = new JsonDocumentOptions() {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            using var stream = File.OpenRead(GetResourcePath(filename));
            return JsonDocument.Parse(stream, opts);
        }
        private static string GetResourcePath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, "Resources", filename);
        }
    }
}
