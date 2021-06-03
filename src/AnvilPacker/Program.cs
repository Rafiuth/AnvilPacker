using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnvilPacker.Cli;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Encoder;
using AnvilPacker.Encoder.PNbt;
using AnvilPacker.Encoder.Transforms;
using AnvilPacker.Level;
using AnvilPacker.Util;
using CommandLine;
using CommandLine.Text;
using NLog;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AnvilPacker
{
    public partial class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private class DisplayTaskProgress
        {
            public PackerTaskProgress Task;
            public TimeSpan ETA = TimeSpan.MaxValue;

            private Queue<(TimeSpan Time, double ItemsProc)> _progSamples = new(64);
            private double _lastProcItems;
            private ProgressBar _pbar;
            private MutableText _countLabel;
            private MutableText _speedLabel;

            public DisplayTaskProgress(PackerTaskProgress task)
            {
                Task = task;
                _pbar = new();
                _countLabel = new MutableText("?/?");
                _speedLabel = new MutableText("0.0 items/s");
            }

            public void Update(TimeSpan time)
            {
                //rounding because the comparison below may fail sometimes.
                double processedItems = Math.Round(Task.ProcessedItems, 6);
                int totalItems = Math.Max(1, Task.TotalItems);
                bool done = processedItems >= totalItems;

                UpdateAndCalcEta(time, processedItems, totalItems, out double speed, out ETA);

                _pbar.Value = processedItems / totalItems;
                _countLabel.Text = $"{processedItems:0}/{totalItems}";
                _speedLabel.Text = done ? "Done" : $"{speed:0.0} items/s";
            }

            private void UpdateAndCalcEta(TimeSpan time, double procItems, double totalItems, out double speed, out TimeSpan eta)
            {
                //TODO: better ETA algorithm, this one jitters too much
                while (ShouldPopSample()) {
                    _progSamples.Dequeue();
                }
                _progSamples.Enqueue((time, procItems - _lastProcItems));
                _lastProcItems = procItems;

                var firstTime = _progSamples.First().Time;
                double itemsProc = _progSamples.Sum(s => s.ItemsProc);

                speed = itemsProc / (time - firstTime).TotalSeconds;

                if (procItems >= totalItems) {
                    speed = 0;
                    eta = TimeSpan.Zero;
                } else if (double.IsNaN(speed) || speed < 1e-4) {
                    speed = 0;
                    eta = TimeSpan.MaxValue;
                } else {
                    eta = TimeSpan.FromSeconds((totalItems - procItems) / speed);
                }

                bool ShouldPopSample()
                {
                    return _progSamples.Count > 128 || (
                               _progSamples.TryPeek(out var firstSample) && 
                                (firstSample.Time - time).TotalSeconds > 30
                            );
                }
            }

            public IRenderable[] CreateGridRow()
            {
                return new IRenderable[] {
                    new Paragraph(Task.Name),
                    _pbar,
                    _countLabel,
                    _speedLabel
                };
            }
        }

        static void ShowProgress(PackerTaskProgress[] packerTasks, Task completionTask)
        {
            if (!AnsiConsole.Profile.Capabilities.Interactive) {
                //Live() will throw in consoles that don't have cursor
                AnsiConsole.WriteLine("Non interactive console detected, progress not displayed...");
                completionTask.Wait();
                return;
            }
            var sw = Stopwatch.StartNew();
            var tasks = packerTasks.Select(t => new DisplayTaskProgress(t)).ToArray();

            var grid = new Grid();
            var elapsedText = new MutableText("", new Style(Color.Grey));
            var etaText = new MutableText("", new Style(Color.Grey));

            for (int i = 0; i < 4; i++) {
                grid.AddColumn();
            }
            foreach (var task in tasks) {
                grid.AddRow(task.CreateGridRow());
            }
            grid.AddRow(new Paragraph("Elapsed"), elapsedText);
            grid.AddRow(new Paragraph("ETA"), etaText);

            var target = new Padder(grid, new Padding(0, 1));

            AnsiLogTarget.BufferLogs = true;

            AnsiConsole
                .Live(target)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Crop)
                .Cropping(VerticalOverflowCropping.Bottom)
                .Start(ctx => {
                    int seenComplete = 0; //used to refresh after task is done, so the text is updated to 100%

                    while (!completionTask.IsCompleted || seenComplete++ == 0) {
                        double etaSecs = 0; //not using TimeSpan directly because it may overflow and throw
                        foreach (var task in tasks) {
                            task.Update(sw.Elapsed);
                            etaSecs += task.ETA.TotalSeconds;
                        }
                        var eta = TimeSpan.FromSeconds(Math.Min(etaSecs, TimeSpan.MaxValue.TotalSeconds));
                        elapsedText.Text = CliUtils.FormatTime(sw.Elapsed);
                        etaText.Text = CliUtils.FormatTime(eta, true);

                        AnsiLogTarget.FlushBuffer();
                        ctx.Refresh();
                        Thread.Sleep(150);
                    }
                });
        }

        static void Main(string[] args)
        {
            var config = new NLog.Config.LoggingConfiguration();
            //var consoleTarget = new NLog.Targets.ConsoleTarget("console");
            //consoleTarget.Layout = "[${level:uppercase=true} ${logger:shortName=true}] ${replace-newlines:replacement=\n:${message}} ${exception:format=toString}";
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, new AnsiLogTarget());
            NLog.LogManager.Configuration = config;

            RegistryLoader.Load();

            using var packer = new WorldPacker(@"../Release/unpacked/", "../Release/world_default1.zip", 2);
            var encTask = packer.Decode();
            ShowProgress(packer.TaskProgresses, encTask);


            /*
                        File.Delete("test.zip");
                        using var packer = new WorldPacker(@"E:\Projects\AnvilPacker\test_data\world_default1", "test.zip", 2);
                        var encTask = packer.Encode();

                        //https://stackoverflow.com/questions/58750002/what-algorithms-exist-for-accurately-estimating-eta-on-data-transfer
                        ShowProgress(packer.TaskProgresses, encTask);*/
            return;

            //args = new string[] { "unpack", "-i", "../Release/mcpworld.zip", "-o", "unpacked/", "-y" };
            //args = new string[] { "pack", "-i", @"E:\Projects\AnvilPacker\test_data\mcpworld", "-o", "mcpworld.zip" };
            args = new string[] { "unpack", "-o", @"unpacked_mcpworld", "-i", "mcpworld.zip" };

            //Bench(@"E:\Projects\AnvilPacker\test_data\Witchcraft and Wizardry\region\r.10.4.mca", @"C:\Program Files\7-Zip-Zstandard\7z.exe");

            //TestDec();
            //return;

            var parser = new CommandLine.Parser(cfg => {
                cfg.CaseInsensitiveEnumValues = true;
                cfg.AutoHelp = true;
            });
            var result = parser.ParseArguments(
                args, 
                typeof(PackOptions), 
                typeof(UnpackOptions), 
                typeof(DumpOptions)
            );
            result
                .WithParsed<PackOptions>(Run)
                .WithParsed<UnpackOptions>(Run)
                .WithParsed<DumpOptions>(Run)
                .WithNotParsed(errs => {
                    var helpText = HelpText.AutoBuild(result, h => {
                        h.AdditionalNewLineAfterOption = false;
                        h.Heading = "AnvilPacker 0.1-alpha";
                        return HelpText.DefaultParsingErrorsHandler(result, h);
                    }, e => e);
                    Console.WriteLine(helpText);
                    //TODO https://github.com/commandlineparser/commandline/wiki/
                });
        }

        private static void Run(PackOptions opts)
        {
            if (!Directory.Exists(opts.Input)) {
                Error($"Input world '{opts.Input}' does not exist.");
            }
            PromptOverwrite(opts.Output, opts.Overwrite, false);

            var packer = new WorldPacker(opts.Input, opts.Output, opts.MaxThreads);
            var task = packer.Encode();
            ShowProgress(packer.TaskProgresses, task);
        }

        private static void Run(UnpackOptions opts)
        {
            if (!File.Exists(opts.Input)) {
                Error($"Input file '{opts.Input}' does not exist.");
            }
            PromptOverwrite(opts.Output, opts.Overwrite, true);

            throw null;
        }

        private static void PromptOverwrite(string path, bool forceOverwrite, bool isDir)
        {
            if (isDir ? !Directory.Exists(path) : !File.Exists(path)) {
                return;
            }
            if (!forceOverwrite) {
                try {
                    if (!AnsiConsole.Profile.Capabilities.Interactive) {
                        throw new Exception();
                    }
                    string text = $"The output {(isDir ? "directory" : "file")} '{path}' already exists. Overwrite?";
                    var prompt = new ConfirmationPrompt(text) {
                        DefaultValue = false
                    };
                    if (!AnsiConsole.Prompt(prompt)) {
                        Environment.Exit(1);
                    }
                } catch {
                    //Workaround for Interactive incorrectly returning true and Prompt() throwing
                    Error($"The output {(isDir ? "directory" : "file")} '{path}' already exists. Use -y to force overwrite.");
                }
            }
            if (isDir) {
                Directory.Delete(path, true);
            } else {
                File.Delete(path);
            }
        }

        private static void Error(string msg)
        {
            AnsiConsole.MarkupLine("[red]{0}[/]", msg);
            Environment.Exit(1);
        }

        private static void TestEnc()
        {
            //string file = @"C:\Users\Daniel\Desktop\Arquivos2\mc\saves\New World\region\r.-1.0.mca";
            //string file = @"../../../test_data/world_amplified1/region/r.-1.-1.mca";
            string file = @"../../../test_data/world_default1/region/r.0.-1.mca";
            //string file = @"../../../test_data\Imperialcity v14.1 converted\region\r.-1.1.mca";
            //string file = @"../../../test_data\Witchcraft and Wizardry\region\r.10.4.mca";
            //string file = @"../../../test_data\nuked1\region\r.0.0.mca";

            var sw = Stopwatch.StartNew();

            var world = new WorldInfo(Path.GetDirectoryName(Path.GetDirectoryName(file)));
            var region = new RegionBuffer();
            region.Load(world, file);

            Console.WriteLine($"Region deserialization took {sw.ElapsedMilliseconds}ms");
            Console.WriteLine("Hash: " + Verifier.HashBlocks(region));

            sw.Restart();
            var encoder = new RegionEncoder(region);
            using var encRegion = new DataWriter(File.Create("encoded.bin"));

            int lastReport = 0;
            var progress = new Progress<double>(p => {
                int now = Environment.TickCount;
                if (now - lastReport > 500) {
                    lastReport = now;
                    Console.WriteLine($"Encoding... {p * 100:0}%");
                }
            });
            encoder.Encode(encRegion, progress);
            
            //Dump(region, "dumped.bin", true, false);
            //DumpImages(region, "layers", false);
            Console.WriteLine($"Encoding took {sw.ElapsedMilliseconds}ms");
        }

        private static void TestDec()
        {
            using var encRegion = new DataReader(File.OpenRead("encoded.bin"));

            var sw = Stopwatch.StartNew();
            var region = new RegionBuffer();

            int lastReport = 0;
            var progress = new Progress<double>(p => {
                int now = Environment.TickCount;
                if (now - lastReport > 500) {
                    lastReport = now;
                    Console.WriteLine($"Decoding... {p * 100:0}%");
                }
            });

            var dec = new RegionDecoder(region);
            dec.Decode(encRegion, progress);

            Console.WriteLine($"Decoding took {sw.ElapsedMilliseconds}ms");
            Console.WriteLine("Hash: " + Verifier.HashBlocks(region));

            new RegionPrimer(region, TransformPipe.Empty).Prime();

            region.Save(new WorldInfo("unpaked/"), "r.0.0.mca");
        }

        private static void Bench(string regionFile, string sevenzipExec)
        {
            string[] files = { 
                "bench/region.mca",     //0
                "bench/chunks.nbt",     //1
                "bench/rawblocks.bin",  //2
                "bench/rawchunks.bin"   //3
            };
            File.Copy(regionFile, files[0]);
            DumpNbt(regionFile, files[1]);
            DumpBlocks(regionFile, files[2]);
            DumpRawChunks(regionFile, files[3]);

            var sb = new StringBuilder();
            foreach (var file in files) {
                var name = Path.GetFileNameWithoutExtension(file);
                //https://superuser.com/a/1449735
                (string Meth, Process Proc)[] procs = {
                    ("deflate", Process.Start(sevenzipExec, $"a -mm=Deflate -mx=9 bench/{name}_deflate.7z {file}")),
                    ("brotli",  Process.Start(sevenzipExec, $"a -m0=BROTLI -mx=11 bench/{name}_brotli.7z {file}")),
                    ("lzma2",   Process.Start(sevenzipExec, $"a -t7z -m0=lzma2 -mx=9 -mfb=273 -ms -md=31 -myx=9 -mtm=- -mmt -mmtf -md=1536m -mmf=bt3 -mmc=10000 -mpb=0 -mlc=0 bench/{name}_lzma2.7z {file}")),
                };
                while (!procs.All(p => p.Proc.HasExited)) {
                    Thread.Sleep(500);
                }

                sb.Append($"| {name,12} | {FormatLen(file)}");
                foreach (var (meth, _) in procs) {
                    sb.Append(FormatLen($"bench/{name}_{meth}.7z"));
                }
                sb.AppendLine();

                string FormatLen(string fn) => $"{new FileInfo(fn).Length / 1024,9}KB |";
            }
            Console.WriteLine("-------\n\n" + sb);
        }

        private static void DumpImages(RegionBuffer buf, string path, bool gray)
        {
            //ffmpeg -i %d.ppm png/%d.png
            Directory.CreateDirectory(path);

            var header = Encoding.ASCII.GetBytes($"P{(gray?5:6)}\n512 512 255\n");
            int bpp = gray ? 1 : 3;
            byte[] data = new byte[header.Length + (512 * 512) * bpp];
            header.CopyTo(data, 0);

            var palette = new Dictionary<int, int>();

            new HiddenBlockRemovalTransform().Apply(buf);

            for (int y = 0; y < 256; y++) {
                for (int z = 0; z < 512; z++) {
                    for (int x = 0; x < 512; x++) {
                        var chunk = buf.GetChunk(x >> 4, z >> 4);
                        var block = chunk == null ? BlockRegistry.Air : chunk.GetBlock(x & 15, y, z & 15);
                        if (!palette.TryGetValue(block.Id, out int color)) {
                            palette[block.Id] = gray ? palette.Count : 
                                                block.Material.Color.ToRgb();
                            if (gray && palette.Count > 256) {
                                throw new NotSupportedException("Region with more than 256 colors");
                            }
                        }
                        int i = header.Length + (x + z * 512) * bpp;
                        if (gray) {
                            data[i] = (byte)color;
                        } else {
                            data[i + 0] = (byte)(color >> 16);
                            data[i + 1] = (byte)(color >> 8);
                            data[i + 2] = (byte)(color >> 0);
                        }
                    }
                }
                File.WriteAllBytes($"{path}/{y}.ppm", data);
            }
        }

        private static void RenderHeightmap(RegionBuffer region)
        {
            byte[] pixels = new byte[512 * 512 * 3];
            int[] palette = region.Palette.ToArray(b => b.Material.Color.ToRgb());

            short[] heights = new short[512 * 512];
            short[] buf = new short[16 * 16];

            var heightComputer = new HeightMapComputer(region, HeightMapType.MotionBlocking);

            foreach (var chunk in region.Chunks.ExceptNull()) {
                heightComputer.Compute(chunk, buf);
                int cx = chunk.X & 31;
                int cz = chunk.Z & 31;

                for (int bz = 0; bz < 16; bz++) {
                    for (int bx = 0; bx < 16; bx++) {
                        int index = (cx * 16 + bx) + (cz * 16 + bz) * 512;
                        heights[index] = (short)(buf[bx + bz * 16] - 1);
                    }
                }
            }

            foreach (var chunk in region.Chunks.ExceptNull()) {
                int cx = chunk.X & 31;
                int cz = chunk.Z & 31;

                for (int bz = 0; bz < 16; bz++) {
                    for (int bx = 0; bx < 16; bx++) {
                        int index = (cx * 16 + bx) + (cz * 16 + bz) * 512;
                        int height = heights[index];
                        int color = palette[chunk.GetSection(height >> 4).GetBlockId(bx, height & 15, bz)];
                        color = Emboss(cx * 16 + bx, cz * 16 + bz, color);
                        pixels[index * 3 + 0] = (byte)(color >> 16);
                        pixels[index * 3 + 1] = (byte)(color >> 8);
                        pixels[index * 3 + 2] = (byte)(color >> 0);
                    }
                }
            }
            using var fs = File.Create("heightmap.ppm");
            fs.Write(Encoding.ASCII.GetBytes($"P6\n512 512 255\n"));
            fs.Write(pixels);

            int Emboss(int x, int z, int color)
            {
                float rad = 225 * MathF.PI / 180;
                float sin = MathF.Sin(rad);
                float cos = MathF.Cos(rad);

                int mx = GetHeight((x - 1), z);
                int px = GetHeight((x + 1), z);
                int mz = GetHeight(x, (z - 1));
                int pz = GetHeight(x, (z + 1));

                float br = 1f + Math.Clamp((mx - px) * sin + (mz - pz) * cos, -4f, 3f) * 0.1f;

                int r = (int)((color >> 16 & 255) * br);
                int g = (int)((color >> 8 & 255) * br);
                int b = (int)((color >> 0 & 255) * br);
                r = r < 0 ? 0 : r > 255 ? 255 : r;
                g = g < 0 ? 0 : g > 255 ? 255 : g;
                b = b < 0 ? 0 : b > 255 ? 255 : b;
                return 255 << 24 | r << 16 | g << 8 | b << 0;
            }
            int GetHeight(int x, int z)
            {
                x = Math.Clamp(x, 0, 511);
                z = Math.Clamp(z, 0, 511);
                return heights[x + z * 512];
            }
        }
    }
}
