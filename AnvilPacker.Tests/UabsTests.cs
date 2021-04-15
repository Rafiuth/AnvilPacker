using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data.Entropy;
using Xunit;

namespace AnvilPacker.Tests
{
    public class UabsTests
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
            p0 = (int)(p0 * (long)UAbsConsts.K / data.Length);

            var enc = new UAbsEncoder();
            enc.Init();

            for (int i = data.Length - 1; i >= 0; i--) {
                enc.Write(data[i], p0);
            }
            var encData = enc.Finish();
            //Console.WriteLine($"Encoded size: {encData.Count} Ratio: {encData.Count / (data.Length / 8.0)}");

            var dec = new UAbsDecoder();
            dec.Init(encData.Array, encData.Offset);

            for (int i = 0; i < data.Length; i++) {
                int expected = data[i] ? 1 : 0;
                int actual = dec.Read(p0);
                Assert.Equal(expected, actual);
            }
        }
    }
}
