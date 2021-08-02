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
            for (int bufSize = 0; bufSize < 64; bufSize += 16) {
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
            for (int bufSize = 0; bufSize < 64; bufSize += 16) {
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
            for (int bufSize = 0; bufSize < 64; bufSize += 16) {
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

                reader.Position = 0;
                reader.SkipBytes(8);
                Assert.Equal("The quick brown fox jumped over the lazy dog", reader.ReadNulString());
            }
        }

        [Fact]
        public void TestAsStream()
        {
            for (int bufSize = 0; bufSize < 64; bufSize += 16) {
                var mem = new MemoryStream();
                var writer = new DataWriter(mem, true, bufSize);

                writer.WriteIntLE(1234);
                writer.WriteFloatLE(MathF.PI);
                writer.WriteNulString("The quick brown fox jumped over the lazy dog");
                writer.WriteIntLE(5678);
                writer.Flush();

                mem.Position = 0;
                var reader = new DataReader(mem, true, bufSize);
                int dataLen = (int)mem.Length - 5;
                using (var stream = reader.AsStream(dataLen + 1)) {
                    var buf = new byte[dataLen];
                    int read1 = stream.Read(buf);

                    Assert.Equal(dataLen, read1);
                    Assert.Equal(mem.ToArray()[0..dataLen], buf[0..dataLen]);
                }
                Assert.Equal(5678, reader.ReadIntLE());
            }
        }

        [Fact]
        public void TestSlice()
        {
            var data = new byte[256];
            for (int i = 0; i < data.Length; i++) {
                data[i] = (byte)i;
            }
            var reader = new DataReader(new MemoryStream(data));

            var r1 = reader.Slice(128);
            var b1 = r1.ReadBytes(64);
            Assert.Equal(b1, data[0..64]);

            var r2 = r1.Slice(64);
            var b2 = r2.ReadBytes(64);
            Assert.Equal(b2, data[64..128]);
            Assert.ThrowsAny<Exception>(() => r2.ReadByte());

            var r3 = reader.Slice(64);
            var b3 = r3.ReadBytes(32);
            Assert.Equal(b3, data[128..160]);

            var b4 = r3.ReadBytes(32);
            Assert.Equal(b4, data[160..192]);

            var b5 = reader.ReadBytes(64);
            Assert.Equal(b5, data[192..256]);
        }
    }
}
