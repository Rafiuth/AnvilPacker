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
            var prob = new int[1024 * 256 * 8];

            var rng = new Random(1234);
            for (int i = 0; i < data.Length; i++) {
                data[i] = rng.Next(3) == 0;
                prob[i] = rng.Next(ArithmCoderConsts.K);
                if (!data[i] && prob[i] == 0) {
                    prob[i]++;
                }
            }

            var writer = new MemoryDataWriter();
            var enc = new ArithmEncoder(writer);

            for (int i = 0; i < data.Length; i++) {
                enc.Write(data[i], prob[i]);
            }
            enc.Flush();
            writer.Position = 0;

            var dec = new ArithmDecoder(new DataReader(writer.BaseStream));

            for (int i = 0; i < data.Length; i++) {
                int expected = data[i] ? 1 : 0;
                int actual = dec.Read(prob[i]);
                Assert.Equal(expected, actual);
            }
        }
    }
}
