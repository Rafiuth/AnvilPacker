using System;
using System.Collections.Generic;
using AnvilPacker.Level;

namespace AnvilPacker.Encoder.Transforms
{
    public class TransformPipe
    {
        public static TransformPipe Empty { get; } = new();

        //Pipe string: hidden_block_removal[samples=64,radius=3,cum_freqs=false],!predict_upgrade_data
        public IReadOnlyList<TransformBase> Transforms { get; }

        public TransformPipe(params TransformBase[] transforms)
        {
            Transforms = transforms;
        }

        public void Apply(RegionBuffer region)
        {
            throw new NotImplementedException();
        }
        public void Reverse(RegionBuffer region)
        {
            throw new NotImplementedException();
        }

        public static void RegisterTransforms()
        {
            Register<HiddenBlockRemovalTransform>("hidden_block_removal")
                .AddSetting("samples",      t => ref t.Samples)
                .AddSetting("radius",       t => ref t.Radius)
                .AddSetting("cum_freqs",    t => ref t.CummulativeFreqs);

            Register<UpgradeDataTransform>("predict_upgrade_data");
        }

        private static RegisteredTransform<T> Register<T>(string name) where T : TransformBase
        {
            return null;
        }

        class RegisteredTransform<T> where T : TransformBase
        {
            public delegate ref V RefGetter<V>(T val);

            public RegisteredTransform<T> AddSetting<V>(string name, RefGetter<V> field)
            {
                return this;
            }
        }
    }
}