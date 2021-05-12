using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AnvilPacker.Level
{
    public class RegistryLoader
    {
        public static void LoadLegacy()
        {
            throw new NotImplementedException();
        }
        public static void Load()
        {
            using var reader = new JsonTextReader(new StreamReader("Resources/blocks.json", Encoding.UTF8));
            JObject json = JObject.Load(reader);
            LoadBlocks(json);
        }
        private static void LoadBlocks(JObject json)
        {
            int stateCount = json["numBlockStates"].Value<int>();
            var arr = (JArray)json["blocks"];

            var states = new BlockState[stateCount];
            var blocks = new ResourceRegistry<Block>(arr.Count);

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in arr) {
                var name = ResourceName.Parse(jb["name"].Value<string>());
                int minStateId = jb["minStateId"].Value<int>();
                int maxStateId = jb["maxStateId"].Value<int>();
                int defaultStateId = jb["defaultStateId"].Value<int>();
                var props = ParseProperties(jb["properties"], propCache);
                var materialName = jb["material"].Value<string>();

                var block = new Block() {
                    Name = name,
                    MinStateId = minStateId,
                    MaxStateId = maxStateId,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName]
                };

                var stateFlags = jb["states"]["flags"];
                var stateLight = jb["states"]["light"]; //packed light info, luminance << 4 | opacity

                for (int id = minStateId; id <= maxStateId; id++) {
                    int i = id - minStateId;
                    var flags = (stateFlags is JArray ? stateFlags[i] : stateFlags).Value<int>();
                    var light = (stateLight is JArray ? stateLight[i] : stateLight).Value<int>();

                    states[id] = new BlockState() {
                        Id = id,
                        Block = block,
                        Attributes = (BlockAttributes)flags,
                        Opacity = (byte)(light & 15),
                        Emittance = (byte)(light >> 4),
                        Properties = props.ToDictionary(
                            p => p.Name, 
                            p => BlockPropertyValue.Create(props, p, i)
                        )
                    };
                }

                block.DefaultState = states[defaultStateId];
                blocks.Add(name, block);
            }
            Block.Registry = blocks.Freeze();
            Block.StateRegistry = new IndexableMap<BlockState>(states);

            BlockState.Air = blocks["air"].DefaultState;
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
    }
}
