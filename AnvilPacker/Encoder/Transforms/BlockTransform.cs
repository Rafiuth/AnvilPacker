using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Encoder.Transforms
{
    public abstract class BlockTransform
    {
        public abstract void Apply(CodingUnit unit);

        public static void RegisterTransforms()
        {
            Register<HiddenBlockRemovalTransform>("hidden_removal")
                .AddSetting("samples",   t => ref t.Samples)
                .AddSetting("radius",    t => ref t.Radius)
                .AddSetting("cum_freqs", t => ref t.CummulativeFreqs);
        }

        private static RegisteredTransform<T> Register<T>(string name) where T : BlockTransform
        {
            return null;
        }

        class RegisteredTransform<T> where T : BlockTransform
        {
            public delegate ref V RefGetter<V>(T val);

            public RegisteredTransform<T> AddSetting<V>(string name, RefGetter<V> field)
            {
                return this;
            }
        }
    }
}
