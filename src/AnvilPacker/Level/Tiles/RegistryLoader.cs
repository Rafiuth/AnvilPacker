using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace AnvilPacker.Level
{
    public class RegistryLoader
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        public static void Load()
        {
            var sw = Stopwatch.StartNew();

            LoadBlocks();
            LoadLegacyBlocks();

            BlockRegistry.Air = BlockRegistry.GetBlock("air").DefaultState;

            sw.Stop();
            _logger.Debug($"Block registry loaded in {sw.ElapsedMilliseconds}ms");
        }

        private static void LoadBlocks()
        {
            using var reader = new JsonTextReader(new StreamReader(GetResourcePath("blocks.json"), Encoding.UTF8));
            JObject json = JObject.Load(reader);

            var arr = (JArray)json["blocks"];

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in arr) {
                var name = ResourceName.Parse(jb["name"].Value<string>());
                int minStateId = jb["minStateId"].Value<int>();
                int maxStateId = jb["maxStateId"].Value<int>();
                int defaultStateId = jb["defaultStateId"].Value<int>();
                var props = ParseProperties(jb["properties"], propCache);
                var materialName = jb["material"].Value<string>();

                var states = new BlockState[maxStateId - minStateId + 1];
                var block = new Block() {
                    Name = name,
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName],
                    IsKnown = true
                };

                var stateFlags = jb["states"]["flags"];
                var stateLight = jb["states"]["light"]; //packed light info, luminance << 4 | opacity

                for (int id = minStateId; id <= maxStateId; id++) {
                    int i = id - minStateId;
                    var flags = (stateFlags is JArray ? stateFlags[i] : stateFlags).Value<int>();
                    var light = (stateLight is JArray ? stateLight[i] : stateLight).Value<int>();

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

        private static void LoadLegacyBlocks()
        {
            using var reader = new JsonTextReader(new StreamReader(GetResourcePath("legacy_blocks.json"), Encoding.UTF8));
            JObject json = JObject.Load(reader);
            var arr = (JArray)json["blocks"];

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in arr) {
                var name = ResourceName.Parse(jb["name"].Value<string>());
                int id = jb["id"].Value<int>();
                int defaultStateId = jb["defaultStateId"].Value<int>();
                var props = ParseProperties(jb["properties"], propCache);
                var materialName = jb["material"].Value<string>();
                var states = new BlockState[16];

                var block = new Block() {
                    Name = name,
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName],
                    IsKnown = true
                };

                var stateFlags = jb["states"]["flags"];
                var stateLight = jb["states"]["light"]; //packed light info, luminance << 4 | opacity
                var stateProps = jb["states"]["states"];

                for (int m = 0; m < 16; m++) {
                    int stateId = id << 4 | m;
                    var flags = (stateFlags is JArray ? stateFlags[m] : stateFlags).Value<int>();
                    var light = (stateLight is JArray ? stateLight[m] : stateLight).Value<int>();
                    var rawVals = stateProps is JArray pa ? (m < pa.Count ? pa[m].Value<string>() : null) : stateProps.Value<string>();

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

        private static List<BlockProperty> ParseProperties(JToken jprops, HashSet<BlockProperty> cache)
        {
            var props = new List<BlockProperty>();
            foreach (JObject jp in jprops) {
                var prop = ParseProperty(jp);
                if (!cache.TryGetValue(prop, out var cachedProp)) {
                    cache.Add(prop);
                    cachedProp = prop;
                }
                props.Add(cachedProp);
            }
            return props;
        }
        private static BlockProperty ParseProperty(JObject obj)
        {
            var name = obj["name"].Value<string>();
            var type = obj["type"].Value<string>();

            switch (type) {
                case "bool": {
                    return new BoolProperty(name);
                }
                case "int": {
                    int min = obj["min"].Value<int>();
                    int max = obj["max"].Value<int>();
                    return new IntProperty(name, min, max);
                }
                case "enum": {
                    var values = obj["values"].ToObject<string[]>();
                    return new EnumProperty(name, values);
                }
                default: throw new NotSupportedException($"Unknown property type '{type}'");
            }
        }

        private static string GetResourcePath(string filename)
        {
            return Path.Combine(AppContext.BaseDirectory, "Resources", filename);
        }
    }
}
