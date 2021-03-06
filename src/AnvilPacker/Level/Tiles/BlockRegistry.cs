using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker.Level
{
    using BlockPropertyValue = KeyValuePair<string, string>;

    public class BlockRegistry
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        private static BlockPropertyValue[] _emptyPropVals => Array.Empty<BlockPropertyValue>();
        private static int _nextBlockId = 0, _nextStateId = 0;

        public const DataVersion LatestVersion = (DataVersion)int.MaxValue;

        public static Dictionary<ResourceName, Block> KnownBlocks { get; } = new(1024);
        public static DictionarySlim<int, Block> KnownLegacyBlocks { get; } = new(256);
        /// <summary> Map of blocks that depends on version. The lists are sorted by version, in ascending order. </summary>
        /// <remarks> When a block is being looked up, this map is used first, then KnownBlocks. </remarks>
        public static Dictionary<ResourceName, (DataVersion Version, Block Block)[]> KnownVersionedBlocks { get; } = new(256);

        public static BlockState Air { get; internal set; } = null!;

        //Used to register known blocks, to implement fast equality checks.
        internal static int NextBlockId() => _nextBlockId++;
        internal static int NextStateId() => _nextStateId++;

        public static Block GetBlock(ResourceName name, DataVersion version = LatestVersion)
        {
            return TryGetBlock(name, version) ?? 
                   CreateState(name, _emptyPropVals).Block;
        }
        private static Block? TryGetBlock(ResourceName name, DataVersion version)
        {
            Block? block = null;
            DataVersion actualVersion = (DataVersion)(-1);

            if (KnownVersionedBlocks.TryGetValue(name, out var records)) {
                int i = 0;
                //No point in using binary search here, this array will most always have 1~2 elements
                while (i + 1 < records.Length && version > records[i].Version) {
                    i++;
                }
                (actualVersion, block) = records[i];
            }
            if (version > actualVersion && KnownBlocks.TryGetValue(name, out var newBlock)) {
                return newBlock;
            }
            return block;
        }

        /// <summary> Gets or creates the state for the given block and properties. </summary>
        /// <param name="props">The block state property values. Note that the block state will take the ownership of this array. Do not change it after calling this method. </param>
        public static BlockState GetState(ResourceName blockName, BlockPropertyValue[] props, DataVersion version = LatestVersion)
        {
            return FindState(blockName, props, version) ?? 
                   CreateState(blockName, props);
        }
        private static BlockState? FindState(ResourceName blockName, BlockPropertyValue[] props, DataVersion version = LatestVersion)
        {
            var block = GetBlock(blockName, version);
            if (block == null) {
                return null;
            }
            int stateId = 0, shift = 1, i = 0;
            ulong addedProps = 0; //(almost) intersection of `props` and `block.Properties`. bit[i] refers to props[i]
            
            foreach (var prop in block.Properties) {
                int propIndex = Array.FindIndex(props, e => e.Key == prop.Name);
                string value;

                if (propIndex < 0) {
                    value = block.DefaultState.Properties[i].Value;
                } else {
                    value = props[propIndex].Value;
                    addedProps |= 1ul << propIndex;
                }
                if (!prop.TryGetIndex(value, out int valueIndex)) {
                    return null;
                }
                stateId += valueIndex * shift;
                shift *= prop.NumValues;
                i++;
            }
            //abort if `props` has some unknown property
            Ensure.That(props.Length < 64);
            if (addedProps != (1ul << props.Length) - 1) {
                return null;
            }
            return block.States[stateId];
        }

        private static BlockState CreateState(ResourceName blockName, BlockPropertyValue[] props)
        {
            //note: caching those are probably not worth since there's a palette per region,
            //and it would be somewhat complex/annoying as it would need to be thread safe.
            if (_logger.IsTraceEnabled) {
                _logger.Trace($"Creating state for unknown block '{blockName}' with props '[{string.Join(',', props)}]'");
            }
            //make the properties order invariant
            Array.Sort(props, (a, b) => string.CompareOrdinal(a.Key, b.Key));

            var block = new Block() {
                Id = -1,
                Name = blockName,
                Material = BlockMaterial.Unknown,
                Properties = new List<BlockProperty>(0),
                States = new BlockState[1]
            };
            var state = new BlockState() {
                Block = block,
                Id = -1,
                LightOpacity = 15,
                LightEmission = 0,
                Properties = props,
                Attributes = BlockAttributes.None
            };
            block.DefaultState = state;
            block.States[0] = state;

            return state;
        }

        /// <summary> Parses a block state in the form of <c>namespace:block_name[property1=value1,property2=value2,...]</c> </summary>
        public static BlockState ParseState(string str)
        {
            int i = str.IndexOf('[');
            if (i < 0) {
                return GetState(str, _emptyPropVals);
            }
            var blockName = ResourceName.Parse(str[0..i]);
            var props = new List<BlockPropertyValue>();

            i++; //skip '['
            for (; i < str.Length; i++) {
                int iEq = str.IndexOf('=', i);
                int iValEnd = str.IndexOf(',', iEq);
                if (iValEnd < 0) iValEnd = str.Length - 1;

                var propName = str[i..iEq];
                var propVal = str[(iEq + 1)..iValEnd];
                props.Add(new(propName, propVal));

                // expect ',' or ']' if end
                bool isEnd = iValEnd == str.Length - 1;
                char expected = isEnd ? ']' : ',';
                if (str[iValEnd] != expected) {
                    throw new FormatException($"Malformed block state string '{str}': expecting ',' or ']', got '{str[iValEnd]}'");
                }
                i = iValEnd;
            }
            return GetState(blockName, props.ToArray());
        }

        public static BlockState ParseState(CompoundTag tag, DataVersion version)
        {
            ResourceName blockName = tag["Name"].Value<string>();
            var props = _emptyPropVals;

            if (tag.TryGet("Properties", out CompoundTag? propsTag)) {
                props = new BlockPropertyValue[propsTag.Count];
                int i = 0;
                foreach (var (k, v) in propsTag) {
                    props[i++] = new(k, v.Value<string>());
                }
            }
            return GetState(blockName, props, version);
        }

        public static BlockState GetLegacyState(int id)
        {
            if (KnownLegacyBlocks.TryGetValue(id >> 4, out var block)) {
                return block.States[id & 15];
            }
            return CreateLegacyState(id);
        }
        private static BlockState CreateLegacyState(int id)
        {
            int blockId = id >> 4;
            int stateId = id & 15;

            var name = new ResourceName("anvilpacker", "legacy_" + id);
            var state = CreateState(name, _emptyPropVals);
            state.Id = id;
            state.Attributes |= BlockAttributes.Legacy;
            return state;
        }
    }
}