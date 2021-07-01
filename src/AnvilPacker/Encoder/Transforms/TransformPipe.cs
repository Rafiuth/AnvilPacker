#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Level;

namespace AnvilPacker.Encoder.Transforms
{
    public partial class TransformPipe : IEnumerable<TransformBase>
    {
        public static Dictionary<string, Type> KnownTransforms { get; } = new() {
            { "remove_hidden_blocks",       typeof(HiddenBlockRemovalTransform) },
            { "simplify_upgrade_data",      typeof(UpgradeDataTransform)        },
            { "remove_empty_chunks",        typeof(EmptyChunkRemovalTransform)  },
        };

        public static TransformPipe Empty { get; } = new(Enumerable.Empty<TransformBase>());
        
        //Example: "hidden_block_removal{samples=64,radius=3,cum_freqs=false,whitelist=['stone',dirt,4]},predict_upgrade_data"
        //Syntax is similar to JSON5
        private static readonly SettingParser _parser = new SettingParser(
            rootType: typeof(TransformPipe), 
            types: KnownTransforms, 
            converters: new[] { BlockJsonConverter.Instance }
        );

        public IReadOnlyList<TransformBase> Transforms { get; }

        public int Count => Transforms.Count;

        public TransformPipe(IEnumerable<TransformBase> transforms)
        {
            Transforms = transforms.ToList();
        }

        public void Apply(RegionBuffer region)
        {
            foreach (var transform in Transforms) {
                transform.Apply(region);
            }
        }
        public void Reverse(RegionBuffer region)
        {
            foreach (var transform in Transforms.Reverse().OfType<ReversibleTransform>()) {
                transform.Reverse(region);
            }
        }

        public static TransformPipe Parse(string str)
        {
            return _parser.Parse<TransformPipe>(str);
        }

        public static string GetTransformName(TransformBase transform)
        {
            return KnownTransforms.First(e => e.Value == transform.GetType()).Key;
        }

        public IEnumerator<TransformBase> GetEnumerator() => Transforms.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}