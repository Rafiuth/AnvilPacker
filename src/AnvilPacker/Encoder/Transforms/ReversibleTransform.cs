using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.Transforms
{
    public abstract class ReversibleTransform : TransformBase
    {
        public abstract void Reverse(RegionBuffer region);
    }
}
