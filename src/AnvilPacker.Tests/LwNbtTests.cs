using System;
using System.IO;
using AnvilPacker.Data;
using AnvilPacker.Data.Nbt;
using Xunit;

namespace AnvilPacker.Tests
{
    public class LwNbtTests
    {
        [Fact]
        public void Conformant()
        {
            var data = File.ReadAllBytes("Resources/nbt/conformance1.nbt");
            var tag = NbtDocument.Parse(data, 0, false, out int bytesRead).Root;

            Assert.Equal(123,               tag["byteTest"].AsByte());
            Assert.Equal(short.MaxValue,    tag["shortTest"].AsShort());
            Assert.Equal(int.MaxValue,      tag["intTest"].AsInt());
            Assert.Equal(long.MaxValue,     tag["longTest"].AsLong());
            Assert.Equal(MathF.PI,          tag["floatTest"].AsFloat());
            Assert.Equal(Math.Tau,          tag["doubleTest"].AsDouble());

            Assert.Equal("Lorem ipsum, dolor sit amet. Unicode Fückêrý 👌",          tag["stringTest"].AsString());
            Assert.Equal(new byte[8] { 0, 1, 2, 3, 4, 5, 6, 255 },                   tag["byteArrayTest"].AsByteArray().ToArray());
            Assert.Equal(new int[4] { int.MinValue, 0, int.MaxValue, 1234 },         tag["intArrayTest"].AsIntArray().ToArray());
            Assert.Equal(new long[4] { long.MinValue, 0, long.MaxValue, 123456789 }, tag["longArrayTest"].AsLongArray().ToArray());

            Assert.Equal(2, tag["nestedCompound"].Count);
            Assert.Equal("John Doe", tag["nestedCompound"]["A"]["name"].AsString());
            Assert.Equal(4,          tag["nestedCompound"]["A"]["age"].AsInt());

            Assert.Equal("Jane Doe", tag["nestedCompound"]["B"]["name"].AsString());
            Assert.Equal(5,          tag["nestedCompound"]["B"]["age"].AsInt());

            Assert.Equal(8, tag["compoundList"].Count);
            for (int i = 0; i < 8; i++) {
                int id = (i * 1337) & 127;
                float s = MathF.Cos(i * MathF.PI / 8);

                Assert.Equal(id, tag["compoundList"][i]["id"].AsInt());
                Assert.Equal(s, tag["compoundList"][i]["something"].AsFloat());
            }

            Assert.Equal(16, tag["intList"].Count);
            for (int i = 0; i < 16; i++) {
                Assert.Equal(1 << i, tag["intList"][i].AsInt());
            }
        }
    }
}