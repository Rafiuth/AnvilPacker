using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnvilPacker.Cli;

namespace AnvilPacker
{
    public partial class Program
    {
        private static void RunPacker(PackOptions opts)
        {
            ValidatePaths(opts, true, false);

            using var packer = new WorldPacker(opts.Input, opts.Output);
            var task = packer.Run(opts.MaxThreads);
            ShowPackerProgress(packer, task);
        }

        private static void RunUnpacker(UnpackOptions opts)
        {
            ValidatePaths(opts, false, true);

            using var packer = new WorldUnpacker(opts.Input, opts.Output);
            var task = packer.Run(opts.MaxThreads);
            ShowPackerProgress(packer, task);
        }

        private static void ShowPackerProgress(WorldPackProcessor packer, Task completionTask)
        {
            var sw = Stopwatch.StartNew();

            BufferedConsoleLogTarget.EnableBuffer = true;
            var tasks = packer.TaskProgresses;

            while (!completionTask.IsCompleted) {
                var sb = new StringBuilder();

                sb.Append("\nWorking...\n");

                foreach (var task in tasks) {
                    double procItems = task.ProcessedItems;
                    double totalItems = task.TotalItems;
                    double prog = procItems / totalItems * 100.0;
                    sb.Append($"{task.Name + ":",-15} {prog:0.0}% ({procItems:0} of {totalItems:0} items)\n");
                }
                sb.Append($"Elapsed: {FormatTime(sw.Elapsed)}");

                string str = sb.ToString();
                BufferedConsoleLogTarget.Flush();
                Console.Write(str);

                Thread.Sleep(250);

                //Erase this UI if we are on a terminal
                if (!Console.IsOutputRedirected) {
                    string emptyLine = '\r' + new string(' ', Console.WindowWidth - 1);
                    foreach (var ch in str) {
                        if (ch == '\n') {
                            Console.Write(emptyLine);
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                        }
                    }
                }
            }
            BufferedConsoleLogTarget.EnableBuffer = false;

            if (!completionTask.IsFaulted) {
                _logger.Info($"Done in {FormatTime(sw.Elapsed)}");
            } else {
                _logger.Fatal(completionTask.Exception, "Failed to run packer task");
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            var sb = new StringBuilder();
            if (time.TotalHours >= 1) {
                sb.AppendFormat("{0:0}h ", time.TotalHours);
            }
            if (time.TotalMinutes >= 1) {
                sb.AppendFormat("{0:0}m ", time.Minutes);
            }
            sb.AppendFormat("{0:0}s", time.Seconds);

            return sb.ToString();
        }
    }
}