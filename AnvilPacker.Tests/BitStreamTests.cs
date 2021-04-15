using System;
using System.IO;
using AnvilPacker.Data;
using Xunit;

namespace AnvilPacker.Tests
{
    public class BitStreamTests
    {
        //TODO: more tests

        [Fact]
        public void TestReadWrite()
        {
            var mem = new MemoryStream();
            var bw = new BitWriter(new StreamDataWriter(mem));

            const int COUNT = 65536;

            for (int i = 0; i < COUNT; i++) {
                int bits = 1 + (i % 31);
                bw.WriteBits(i, bits);
            }
            bw.Flush();

            mem.Position = 0;
            var br = new BitReader(mem);
            for (int i = 0; i < COUNT; i++) {
                int bits = 1 + (i % 31);
                int mask = (1 << bits) - 1;
                if (br.ReadBits(bits) != (i & mask)) {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
