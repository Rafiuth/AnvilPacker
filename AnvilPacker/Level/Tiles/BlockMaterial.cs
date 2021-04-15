using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Level
{
    public partial class BlockMaterial
    {
        public static ResourceRegistry<BlockMaterial> Registry { get; } = new(64);

        private static BlockMaterial Reg(string name, MaterialAttributes flags)
        {
            var mat = new BlockMaterial() {
                Name = name,
                Attributes = flags,
            };
            Registry.Add(name, mat);
            return mat;
        }

        public ResourceName Name { get; init; }
        public MaterialAttributes Attributes { get; init; }
        
        public bool BlocksMotion    => Attributes.HasFlag(MaterialAttributes.BlocksMotion);
        public bool IsFlammable     => Attributes.HasFlag(MaterialAttributes.Flammable);
        public bool IsLiquid        => Attributes.HasFlag(MaterialAttributes.Liquid);
        public bool SolidBlocking   => Attributes.HasFlag(MaterialAttributes.SolidBlocking);
        public bool IsReplaceable   => Attributes.HasFlag(MaterialAttributes.Replaceable);
        public bool IsSolid         => Attributes.HasFlag(MaterialAttributes.Solid);

        public override string ToString() => Name.ToString();
    }
    [Flags]
    public enum MaterialAttributes
    {
        BlocksMotion    = 1 << 0,
        Flammable       = 1 << 1,
        Liquid          = 1 << 2,
        SolidBlocking   = 1 << 3, //TODO: wtf does this mean?
        Replaceable     = 1 << 4,
        Solid           = 1 << 5
    }
}
