using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AnvilPacker.Data;
using AnvilPacker.Data.Archives;
using Xunit;

namespace AnvilPacker.Tests
{
    public class DataArchiveTests
    {
        private static byte[] UTF(string s) => Encoding.UTF8.GetBytes(s);
        private static readonly (string Name, byte[] Data)[] Entries = {
            ("lorem.txt",   UTF("Lorem ipsum, dolor sit amet. Nihil vero eos eum dolores porro.")),
            ("fox.txt",     UTF("The quick brown fox jumped over the lazy dog.")),
            ("abc.txt",     UTF("abcdefghijklmnopqrstuvwxyz\nABCDEFGIJKLMNOPQRSTUVWXYZ\n0123456789")),
            ("junk.txt",    UTF("Junk 123\nBinary \x02\x01\n\náéióú\nUnicode: 💁👌🎍😍\nEnd")),
            ("large.bin",   new byte[1024 * 1024 * 8])
        };

        [InlineData("test_archive.zip", typeof(ZipArchiveWriter), typeof(ZipArchiveReader))]
        [InlineData("test_archive_fs", typeof(FileSystemArchiveWriter), typeof(FileSystemArchiveReader))]
        [Theory]
        private void TestArchive(string filename, Type expWriterType, Type expReaderType)
        {
            if (File.Exists(filename)) {
                File.Delete(filename);
            } else if (Directory.Exists(filename)) {
                Directory.Delete(filename, true);
            }

            TestWriter(filename, expWriterType);
            TestReader(filename, expReaderType);
        }

        private void TestReader(string filename, Type expType)
        {
            using var reader = DataArchive.Open(filename);
            Assert.IsType(expType, reader);

            foreach (var entry in reader.ReadEntries()) {
                var data = Entries.First(e => e.Name == entry.Name).Data;

                Assert.Equal(entry.Size, data.Length);

                using var sr = reader.Open(entry.Name);
                var buf = new byte[data.Length];
                int pos = 0;
                while (true) {
                    int read = sr.Read(buf, pos, buf.Length - pos);
                    if (read <= 0) break;
                    pos += read;
                }
                Assert.True(data.AsSpan().SequenceEqual(buf));
            }
        }

        private void TestWriter(string filename, Type expType)
        {
            using var writer = DataArchive.Create(filename);
            Assert.IsType(expType, writer);

            foreach (var (name, data) in Entries) {
                using var sw = writer.Create(name);
                sw.Write(data);
            }
        }
    }
}