#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AnvilPacker.Encoder.Transforms
{
    public partial class TransformPipe : IEnumerable<TransformBase>
    {
        public static TransformPipe Empty { get; } = new(Enumerable.Empty<TransformBase>());

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

        public IEnumerator<TransformBase> GetEnumerator() => Transforms.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}