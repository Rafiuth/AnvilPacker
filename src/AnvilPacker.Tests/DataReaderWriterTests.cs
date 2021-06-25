using System;
using System.IO;
using AnvilPacker.Data;
using Xunit;

namespace AnvilPacker.Tests
{
    public class DataReaderWriterTests
    {
        [Fact]
        public void TestPrims()
        {
            TestPrim<byte>(byte.MaxValue, 0, 1, 2, 69, 127);
            TestPrim<short>(short.MinValue, short.MaxValue, 0, 1, 2, -300, -17000, 300, 17000);
            TestPrim<ushort>(ushort.MaxValue, 0, 1, 2, 300, 17000);
            TestPrim<int>(int.MinValue, int.MaxValue, 0, 1, 2, -300, -48000, 300, 48000);
            TestPrim<uint>(uint.MaxValue, 0, 1, 2, 300, 48000, 5000000);
            TestPrim<long>(long.MinValue, long.MaxValue, 0, 1, 2, -15360000, 300, 48000, 5000000, 15360000);
            TestPrim<ulong>(long.MaxValue, 0, 1, 2, 300, 48000, 5000000, 15360000);
            TestPrim<float>(float.NaN, float.PositiveInfinity, float.MinValue, float.MaxValue, 0, 1, 2, -300.625f, -17000.5f, 300.123f, 17000.47f, MathF.PI);
            TestPrim<double>(double.NaN, double.PositiveInfinity, double.MinValue, double.MaxValue, 0, 1, 2, -300.625, -17000.5, 300.123, 17000.47, Math.PI);
        }

        private void TestPrim<T>(params T[] values) where T : unmanaged
        {
            for (int bufSize = 0; bufSize < 64; bufSize += 64) {
                var mem = new MemoryStream();
                var writer = new DataWriter(mem, true, bufSize);

                foreach (var val in values) {
                    writer.WriteLE<T>(val);
                    writer.WriteBE<T>(val);
                }

                writer.Flush();
                mem.Position = 0;

                var reader = new DataReader(mem, true, bufSize);
                for (int i = 0; i < values.Length; i++) {
                    Assert.Equal(values[i], reader.ReadLE<T>());
                    Assert.Equal(values[i], reader.ReadBE<T>());
                }
            }
        }

        [Fact]
        public void TestStrings()
        {
            string[] strs = {
                "Omnis repellendus possimus aliquam.",
                "Dolor excepturi nam sapiente aut qui voluptatem.",
                "Nobis pariatur voluptatem qui iusto.",
                "Est explicabo error eos."
            };
            for (int bufSize = 0; bufSize < 64; bufSize += 64) {
                var mem = new MemoryStream();
                var writer = new DataWriter(mem, true, bufSize);

                foreach (var str in strs) {
                    writer.WriteString(str, (w, len) => w.WriteIntLE(len));
                    writer.WriteNulString(str);
                }
                writer.Flush();
                mem.Position = 0;

                var reader = new DataReader(mem, true, bufSize);
                for (int i = 0; i < strs.Length; i++) {
                    Assert.Equal(strs[i], reader.ReadString(reader.ReadIntLE()));
                    Assert.Equal(strs[i], reader.ReadNulString());
                }
            }
        }

        [Fact]
        public void TestSeeking()
        {
            for (int bufSize = 0; bufSize < 64; bufSize += 64) {
                var mem = new MemoryStream();
                var writer = new DataWriter(mem, true, bufSize);

                writer.WriteIntLE(1234);
                writer.WriteFloatLE(MathF.PI);
                writer.WriteNulString("The quick brown fox jumped over the lazy dog");
                writer.Flush();

                mem.Position = 0;
                var reader = new DataReader(mem, true, bufSize);
                reader.Position = 4;
                Assert.Equal(MathF.PI, reader.ReadFloatLE());

                reader.Position = 0;
                Assert.Equal(1234, reader.ReadIntLE());

                reader.Position = 8;
                Assert.Equal("The quick brown fox jumped over the lazy dog", reader.ReadNulString());
            }
        }


        [Fact]
        public void TestAsStream()
        {
            for (int bufSize = 0; bufSize < 64; bufSize += 64) {
                var mem = new MemoryStream();
                var writer = new DataWriter(mem, true, bufSize);

                writer.WriteIntLE(1234);
                writer.WriteFloatLE(MathF.PI);
                writer.WriteNulString("The quick brown fox jumped over the lazy dog");
                writer.Flush();

                mem.Position = 0;
                var reader = new DataReader(mem, true, bufSize);
                var stream = reader.AsStream();

                var buf = new byte[mem.Length];
                int read1 = stream.Read(buf);
                int read2 = stream.Read(new byte[64]);

                Assert.Equal(mem.Length, read1);
                Assert.Equal(0, read2);
                Assert.Equal(mem.ToArray(), buf);
            }
        }
    }
}
