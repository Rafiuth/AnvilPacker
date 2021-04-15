using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;

namespace AnvilPacker.Encoder
{
    public class EncoderContext
    {
        public Stream OutStream { get; set; }
        public EncoderOptions Options { get; set; }
    }
}
