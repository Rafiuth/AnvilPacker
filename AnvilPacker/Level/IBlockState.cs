using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level
{
    public interface IBlockState
    {
        /// <summary> Represents an unique ID of this block state. Not guaranteed to be a valid block id in Minecraft. </summary>
        int Id { get; }

        BlockMaterial Material { get; }
    }
    public interface IBlock
    {

    }
}
