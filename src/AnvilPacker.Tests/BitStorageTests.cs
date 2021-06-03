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
        public void TestDataIntact_Packed()
        {
            TestDataIntact((len, bits) => new PackedBitStorage(len, bits));
        }
        [Fact]
        public void TestDataIntact_Sparse()
        {
            TestDataIntact((len, bits) => new SparseBitStorage(len, bits));
        }

        private void TestDataIntact(Func<int, int, IBitStorage> storageFactory)
        {
            var rng = new Random(1234);
            for (int len = 1000; len <= 1024; len++) {
                for (int b = 1; b < 31; b++) {
                    var bs = storageFactory(len, b);

                    for (int i = 0; i < len; i++) {
                        Assert.Equal(0, bs[i]);

                        for (int j = 0; j < 2; j++) {
                            int k = rng.Next(1 << b);
                            bs[i] = k;
                            Assert.Equal(k, bs[i]);
                        }
                    }

                    for (int i = len - 1; i >= 0; i--) {
                        for (int j = 0; j < 2; j++) {
                            int k = rng.Next(1 << b);
                            bs[i] = k;
                            Assert.Equal(k, bs[i]);
                        }
                    }
                }
            }
        }
    }
}
