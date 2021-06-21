using System;
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
        [Fact]
        public void Test()
        {
            if (File.Exists("test_data_archive.zip")) {
                File.Delete("test_data_archive.zip");
            }
            TestArchive("test_data_archive.zip", typeof(ZipArchiveWriter), typeof(ZipArchiveReader));

            if (Directory.Exists("test_data_archive_fs")) {
                Directory.Delete("test_data_archive_fs", true);
            }
            TestArchive("test_data_archive_fs", typeof(FileSystemArchiveWriter), typeof(FileSystemArchiveReader));
        }

        private void TestArchive(string filename, Type expWriterType, Type expReaderType)
        {
            var entries = new (string Name, string Data)[] {
                ("lorem.txt", "Lorem ipsum, dolor sit amet. Nihil vero eos eum dolores porro."),
                ("fox.txt", "The quick brown fox jumped over the lazy dog.")
            };
            using (var writer = DataArchive.Create(filename)) {
                Assert.IsType(expWriterType, writer);

                foreach (var (name, data) in entries) {
                    using var sw = writer.CreateEntry(name);
                    sw.Write(Encoding.UTF8.GetBytes(data));
                }
            }

            using (var reader = DataArchive.Open(filename)) {
                Assert.IsType(expReaderType, reader);

                foreach (var entry in reader.ReadEntries()) {
                    var text = entries.First(e => e.Name == entry.Name).Data;
                    var data = Encoding.UTF8.GetBytes(text);

                    Assert.Equal(entry.Size, data.Length);

                    using var sr = reader.OpenEntry(entry);
                    var buf = new byte[4096];
                    int pos = 0;
                    while (true) {
                        int read = sr.Read(buf, pos, buf.Length - pos);
                        if (read <= 0) break;
                        pos += read;
                    }
                    Assert.Equal(data, buf[0..data.Length]);
                }
            }
        }
    }
}