using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Data;
using Xunit;

namespace AnvilPacker.Tests
{
    public class BitStorageTests
    {
        //TODO: hardcode expected data

        [Fact]
        public void TestDataIntact_Sparse()
        {
            var rng = new Random(1234);
            for (int len = 1000; len <= 1024; len++) {
                for (int b = 1; b < 31; b++) {
                    var bs = new SparseBitStorage(len, b);

                    for (int i = 0; i < len; i++) {
                        Assert.Equal(0, bs.Get(i));

                        for (int j = 0; j < 2; j++) {
                            int k = rng.Next(1 << b);
                            bs.Set(i, k);
                            Assert.Equal(k, bs.Get(i));
                        }
                    }
                }
            }
        }

        [Fact]
        public void TestDataIntact_Packed()
        {
            var rng = new Random(1234);
            for (int len = 1000; len <= 1024; len++) {
                for (int b = 1; b < 31; b++) {
                    var bs = new PackedBitStorage(len, b);

                    for (int i = 0; i < len; i++) {
                        Assert.Equal(0, bs.Get(i));

                        for (int j = 0; j < 2; j++) {
                            int k = rng.Next(1 << b);
                            bs.Set(i, k);
                            Assert.Equal(k, bs.Get(i));
                        }
                    }
                }
            }
        }
    }
}
