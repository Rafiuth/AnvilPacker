using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Encoder.PNbt;
using Xunit;

namespace AnvilPacker.Tests
{
    public class NbtPackerTests
    {
        [Fact]
        public void TestDataSerialization()
        {
            var tag = NbtIO.ReadCompressed("../../../../test_data/nbt/chunks_1.nbt.gz");
            var dw = new MemoryDataWriter();

            var packer = new NbtPacker();
            packer.Add(tag);
            packer.Encode(dw, false);

            dw.Flush();
            dw.BaseStream.Position = 0;
            var dr = new DataReader(dw.BaseStream);
            var unpacker = new NbtUnpacker(dr);
            unpacker.ReadHeader();

            EnsureSchemasEqual(packer._lastSchemas, unpacker._schemas);

            Assert.True(Verifier.CompareTags(tag, unpacker.Read()));
        }

        private void EnsureSchemasEqual(IList<Schema> list1, IList<Schema> list2)
        {
            Assert.Equal(list1.Count, list2.Count);
            for (int i = 0; i < list1.Count; i++) {
                EnsureSchemasEqual(list1[i], list2[i]);
            }
        }

        private void EnsureSchemasEqual(Schema s1, Schema s2)
        {
            Assert.Equal(s1.Fields.Count, s2.Fields.Count);

            var fields2 = s2.Fields.ToDictionary(f => f.Name, f => f);
            foreach (var f1 in s1.Fields) {
                var f2 = fields2[f1.Name];
                EnsureFieldsEqual(f1, f2);
            }
        }

        private void EnsureFieldsEqual(SchemaField f1, SchemaField f2)
        {
            Assert.Equal(f1.Name, f2.Name);
            Assert.Equal(f1.Type, f2.Type);
            Assert.Equal(f1.Data?.GetType(), f2.Data?.GetType());

            if (f1.Data != null) {
                EnsureDataEquals(f1.Data, f2.Data);
            }
        }

        private void EnsureDataEquals(FieldData d1, FieldData d2)
        {
            if (d1 is FieldIntData di1 && d2 is FieldIntData di2) {
                Assert.Equal(di1.Min, di2.Min);
                Assert.Equal(di1.Max, di2.Max);
            } else if (d1 is FieldListData dl1 && d2 is FieldListData dl2) {
                Assert.Equal(dl1.ElemType, dl2.ElemType);
                Assert.Equal(dl1.ElemData == null, dl2.ElemData == null);
                Assert.Equal(dl1.ElemSchema == null, dl2.ElemSchema == null);

                if (dl1.ElemData != null) {
                    EnsureDataEquals(dl1.ElemData, dl2.ElemData);
                }
                if (dl1.ElemSchema != null) {
                    EnsureSchemasEqual(dl1.ElemSchema, dl2.ElemSchema);
                }
                EnsureDataEquals(dl1.Len, dl2.Len);
            } else if (d1 is FieldArrayData da1 && d2 is FieldArrayData da2) {
                EnsureDataEquals(da1.Len, da2.Len);
            } else if (d1 is FieldCompoundData dc1 && d2 is FieldCompoundData dc2) {
                EnsureSchemasEqual(dc1.Type, dc2.Type);
            } else {
                throw new Exception("Unknown field data type");
            }
        }
    }
}
