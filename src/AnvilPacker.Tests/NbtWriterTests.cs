using System;
using System.IO;
using AnvilPacker.Data;
using Xunit;

namespace AnvilPacker.Tests
{
    public class NbtWriterTests
    {
        [Fact]
        public void Conformant()
        {
            var mem = new MemoryDataWriter();
            var nw = new NbtWriter(mem);
            nw.BeginCompound();
            nw.WriteByte("byteTest", 123);
            nw.WriteShort("shortTest", short.MaxValue);
            nw.WriteInt("intTest", int.MaxValue);
            nw.WriteLong("longTest", long.MaxValue);
            nw.WriteFloat("floatTest", MathF.PI);
            nw.WriteDouble("doubleTest", Math.Tau);
            nw.WriteString("stringTest", "Lorem ipsum, dolor sit amet. Unicode Fückêrý 👌");
            nw.WriteArray("byteArrayTest", new byte[8] { 0, 1, 2, 3, 4, 5, 6, 255 });
            nw.WriteArray("intArrayTest", new int[4] { int.MinValue, 0, int.MaxValue, 1234 });
            nw.WriteArray("longArrayTest", new long[4] { long.MinValue, 0, long.MaxValue, 123456789 });

            nw.BeginCompound("nestedCompound");
            {
                nw.BeginCompound("A");
                {
                    nw.WriteString("name", "John Doe");
                    nw.WriteInt("age", 4);
                }
                nw.EndCompound();
                nw.BeginCompound("B");
                {
                    nw.WriteString("name", "Jane Doe");
                    nw.WriteInt("age", 5);
                }
                nw.EndCompound();
            }
            nw.EndCompound();

            nw.BeginList("compoundList", TagType.Compound);
            for (int i = 0; i < 8; i++) {
                nw.BeginCompound();
                nw.WriteInt("id", (i * 1337) & 127);
                nw.WriteFloat("something", MathF.Cos(i * MathF.PI / 8));
                nw.EndCompound();
            }
            nw.EndList();

            nw.BeginList("intList", TagType.Int);
            for (int i = 0; i < 16; i++) {
                nw.WriteInt(1 << i);
            }
            nw.EndList();
            nw.EndCompound();

            Assert.True(mem.BufferSpan.SequenceEqual(File.ReadAllBytes("Resources/NbtWriterTests/conformance1.nbt")));
        }

        [Fact]
        public void StateValidation_UnnamedBeginCompound()
        {
            var writer = new NbtWriter(new MemoryDataWriter());
            writer.BeginCompound(); //root
            Assert.Throws<InvalidOperationException>(() => writer.BeginCompound());
        }
    }
}