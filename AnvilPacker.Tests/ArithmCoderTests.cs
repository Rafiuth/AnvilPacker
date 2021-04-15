using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using Xunit;

namespace AnvilPacker.Tests
{
    public class ArithmCoderTests
    {
        [Fact]
        public void TestDataIntact()
        {
            var data = new bool[1024 * 256 * 8];
            var rng = new Random(1234);
            int p0 = 0;
            for (int i = 0; i < data.Length; i++) {
                data[i] = rng.Next(3) == 0;
                p0 += data[i] ? 0 : 1;
            }
            p0 = (int)(p0 * (long)ArithmCoderConsts.K / data.Length);

            var s = new MemoryStream();

            var enc = new ArithmEncoder(new StreamDataWriter(s));

            for (int i = 0; i < data.Length; i++) {
                enc.Write(data[i], p0);
            }
            enc.Flush();
            //Console.WriteLine($"Encoded size: {s.Length} Ratio: {s.Length / (data.Length / 8.0)}");

            var dec = new ArithmDecoder();
            s.Position = 0;
            dec.Init(s);

            for (int i = 0; i < data.Length; i++) {
                int expected = data[i] ? 1 : 0;
                int actual = dec.Read(p0);
                Assert.Equal(expected, actual);
            }
        }
    }
}
