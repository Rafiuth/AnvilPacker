using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AnvilPacker.Data;
using AnvilPacker.Level.Physics;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level
{
    using BlockPropertyValue = KeyValuePair<string, string>;

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
            LoadVersionedBlocks();
            LoadBlockAliases();
            
            BlockRegistry.Air = BlockRegistry.GetBlock("air").DefaultState;

            sw.Stop();
            _logger.Debug($"Block registry loaded in {sw.ElapsedMilliseconds}ms");
        }

        private static void LoadBlocks()
        {
            using var json = LoadJson("blocks.json");
            var jBlocks = json.RootElement.GetProperty("blocks");
            var jShapes = json.RootElement.GetProperty("shapes");

            var propCache = new HashSet<BlockProperty>();
            var shapes = ParseShapes(jShapes);

            foreach (var jb in jBlocks.EnumerateArray()) {
                ParseBlocks(jb, propCache, shapes, block => {
                    BlockRegistry.KnownBlocks.Add(block.Name, block);
                });
            }
        }
        private static void LoadBlockAliases()
        {
            using var json = LoadJson("block_aliases.jsonc");

            var renames = new Dictionary<ResourceName, List<(DataVersion Version, ResourceName NewName)>>();

            foreach (var jelem in json.RootElement.EnumerateArray()) {
                var version = (DataVersion)jelem.GetInt("version");
                var jrenames = jelem.GetProperty("renames");

                foreach (var entry in jrenames.EnumerateObject()) {
                    var oldName = entry.Name;
                    var newName = entry.Value.GetString()!;
                    var records = renames.GetOrAdd(oldName, () => new());
                    records.Add((version, newName));
                }
            }
            var blockRecords = new List<(DataVersion, Block)>();

            foreach (var (name, records) in renames) {
                records.Sort((a, b) => a.Version - b.Version);
                blockRecords.Clear();

                foreach (var (version, newName) in records) {
                    var latestName = LatestName(version, newName);
                    var block = BlockRegistry.KnownBlocks[latestName];
                    var renamedBlock = block.Rename(name);

                    blockRecords.Add((version, renamedBlock));
                }
                BlockRegistry.KnownVersionedBlocks.Add(name, blockRecords.ToArray());
            }

            ResourceName LatestName(DataVersion version, ResourceName name)
            {
                while (renames.TryGetValue(name, out var records)) {
                    var record = records[^1];
                    if (record.Version <= version) break;
                    
                    (version, name) = record;
                }
                return name;
            }
        }
        private static void LoadVersionedBlocks()
        {
            using var json = LoadJson("blocks_versioned.jsonc");
            var jBlocks = json.RootElement.GetProperty("blocks");
            var jShapes = json.RootElement.GetProperty("shapes");

            var propCache = new HashSet<BlockProperty>();
            var shapes = ParseShapes(jShapes);

            foreach (var jb in jBlocks.EnumerateArray()) {
                var version = (DataVersion)jb.GetInt("version_removed") - 1;
                ParseBlocks(jb, propCache, shapes, block => {
                    var entry = (version, block);

                    var dict = BlockRegistry.KnownVersionedBlocks;
                    if (!dict.TryGetValue(block.Name, out var arr)) {
                        dict.Add(block.Name, new[] { entry });
                    } else {
                        //Insert the entry at the appropriate (sorted) index.
                        //This should be rare, so maybe not worth using a list.
                        var newArr = new (DataVersion, Block)[arr.Length + 1];
                        int i = 0;
                        for (; i < arr.Length; i++) {
                            Ensure.That(arr[i].Version != version, "Cannot have multiple blocks with the same version");
                            if (arr[i].Version > version) break;
                        }
                        newArr[i] = entry;
                        //arr: 0 * 1 2 3
                        //idx:   1
                        if (i > 0) { //lower half, [0]
                            Array.Copy(arr, 0, newArr, 0, i);
                        }
                        if (i < arr.Length) { //upper half, [1..3]
                            Array.Copy(arr, i, newArr, i + 1, arr.Length - i);
                        }
                        dict[block.Name] = newArr;
                    }
                });
            }
        }
        private static void ParseBlocks(JsonElement jb, HashSet<BlockProperty> propCache, List<VoxelShape> shapes, Action<Block> consume)
        {
            var jNames = jb.GetProperty("names");
            int numStates = jb.GetInt("numStates");
            int defaultStateId = jb.GetInt("defaultStateId");
            var props = ParseProperties(jb.GetProperty("properties"), propCache);
            var materialName = jb.GetString("material");

            var jStates = jb.GetProperty("states");
            var jFlags = jStates.GetProperty("flags");
            var jLight = jStates.GetProperty("light"); //packed light info, luminance << 4 | opacity
            var jOcclShapes = jStates.GetProperty("occlusionShapes");

            bool jFlagsIsArr = jFlags.ValueKind == JsonValueKind.Array;
            bool jLightIsArr = jLight.ValueKind == JsonValueKind.Array;
            bool jOcclShapesIsArr = jOcclShapes.ValueKind == JsonValueKind.Array;

            foreach (var jname in jNames.EnumerateArray()) {
                var states = new BlockState[numStates];
                var block = new Block() {
                    Id = BlockRegistry.NextBlockId(),
                    Name = ResourceName.Parse(jname.GetString()!),
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName]
                };

                for (int id = 0; id < numStates; id++) {
                    int flags = (jFlagsIsArr ? jFlags[id] : jFlags).GetInt32();
                    int light = (jLightIsArr ? jLight[id] : jLight).GetInt32();
                    var occlShapeId = (jOcclShapesIsArr ? jOcclShapes[id] : jOcclShapes).GetInt32();

                    states[id] = new BlockState() {
                        Id = BlockRegistry.NextStateId(),
                        Block = block,
                        Attributes = (BlockAttributes)flags,
                        LightOpacity = (byte)(light & 15),
                        LightEmission = (byte)(light >> 4),
                        OcclusionShape = shapes[occlShapeId],
                        Properties = CreatePropertyValues(props, id)
                    };
                }
                block.DefaultState = states[defaultStateId];
                consume(block);
            }
        }
        private static List<VoxelShape> ParseShapes(JsonElement ja)
        {
            var list = new List<VoxelShape>();
            foreach (var jshape in ja.EnumerateArray()) {
                var boxes = new Box8[jshape.GetArrayLength() / 6];
                for (int i = 0; i < boxes.Length; i++) {
                    boxes[i] = new Box8(
                        jshape[i * 6 + 0].GetSByte(),
                        jshape[i * 6 + 1].GetSByte(),
                        jshape[i * 6 + 2].GetSByte(),
                        jshape[i * 6 + 3].GetSByte(),
                        jshape[i * 6 + 4].GetSByte(),
                        jshape[i * 6 + 5].GetSByte()
                    );
                }
                list.Add(new VoxelShape(boxes));
            }
            return list;
        }

        private static void LoadLegacyBlocks()
        {
            using var json = LoadJson("legacy_blocks.json");
            var jArr = json.RootElement.GetProperty("blocks");

            var propCache = new HashSet<BlockProperty>();

            foreach (var jb in jArr.EnumerateArray()) {
                var name = ResourceName.Parse(jb.GetString("name"));
                int id = jb.GetInt("id");
                int defaultStateId = jb.GetInt("defaultStateId");
                var props = ParseProperties(jb.GetProperty("properties"), propCache);
                var materialName = jb.GetString("material");
                var states = new BlockState[16];

                var block = new Block() {
                    Id = BlockRegistry.NextBlockId(),
                    Name = name,
                    States = states,
                    Properties = props,
                    Material = BlockMaterial.Registry[materialName]
                };

                var jStates = jb.GetProperty("states");
                var jFlags = jStates.GetProperty("flags");
                var jLight = jStates.GetProperty("light"); //packed light info, luminance << 4 | opacity
                var jProps = jStates.GetProperty("states");
                bool jFlagsIsArr = jFlags.ValueKind == JsonValueKind.Array;
                bool jLightIsArr = jLight.ValueKind == JsonValueKind.Array;
                bool jPropsIsArr = jProps.ValueKind == JsonValueKind.Array;

                for (int m = 0; m < 16; m++) {
                    int stateId = id << 4 | m;
                    var flags = (jFlagsIsArr ? jFlags[m] : jFlags).GetInt();
                    var light = (jLightIsArr ? jLight[m] : jLight).GetInt();
                    var rawVals = jPropsIsArr ? (m < jProps.GetArrayLength() ? jProps[m].GetString() : null) : jProps.GetString();

                    states[m] = new BlockState() {
                        Id = stateId,
                        Block = block,
                        Attributes = (BlockAttributes)flags | BlockAttributes.Legacy,
                        LightOpacity = (byte)(light & 15),
                        LightEmission = (byte)(light >> 4)
                    };
                    if (rawVals != null) {
                        var vals = rawVals.Split(',');
                        states[m].Properties = props.Select((p, i) => new BlockPropertyValue(p.Name, vals[i])).ToArray();
                    }
                }
                block.DefaultState = states[defaultStateId & 15];
                BlockRegistry.KnownLegacyBlocks.Add(id, block);
            }
        }

        private static BlockPropertyValue[] CreatePropertyValues(List<BlockProperty> props, int stateId)
        {
            var vals = new BlockPropertyValue[props.Count];
            int shift = 1, i = 0;
            foreach (var prop in props) {
                int valIndex = (stateId / shift) % prop.NumValues;
                vals[i] = new(prop.Name, prop.GetValue(valIndex));
                shift *= prop.NumValues;
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
                        values[i] = jvals[i].GetString()!;
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
