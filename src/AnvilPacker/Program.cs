using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AnvilPacker.Cli;
using AnvilPacker.Data;
using AnvilPacker.Encoder;
using AnvilPacker.Level;
using AnvilPacker.Util;
using NLog;

namespace AnvilPacker
{
    public partial class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            var opts = new PackOptions() {
                Input = @"E:\Projects\AnvilPacker\test_data\mcpworld",
                Output = "test.zip",
                MaxThreads = 1,
                LogLevel = LogLevel.Debug,
                Overwrite = false
            };
            Init(opts);

            RunPacker(opts);
        }

        private static void Init(CliOptions? opts)
        {
            var config = new NLog.Config.LoggingConfiguration();
#if DEBUG
            var minLogLevel = LogLevel.Debug;
#else
            var minLogLevel = opts?.LogLevel ?? LogLevel.Info; 
#endif
            //var consoleTarget = new NLog.Targets.ConsoleTarget("console");
            //consoleTarget.Layout = "[${level:uppercase=true} ${logger:shortName=true}] ${replace-newlines:replacement=\n:${message}} ${exception:format=toString}";
            config.AddRule(minLogLevel, LogLevel.Fatal, BufferedConsoleLogTarget.Instance);
            NLog.LogManager.Configuration = config;

            RegistryLoader.Load();
        }

        private static void ValidatePaths(CliOptions opts, bool inIsDir, bool outIsDir)
        {
            if (inIsDir ? !Directory.Exists(opts.Input) : !File.Exists(opts.Input)) {
                Error($"Input {(inIsDir ? "directory" : "file")} '{opts.Input}' does not exist.");
            }
            PromptOverwrite(opts.Output, opts.Overwrite, outIsDir);
        }
        private static void PromptOverwrite(string path, bool forceOverwrite, bool isDir)
        {
            if (isDir ? !Directory.Exists(path) : !File.Exists(path)) {
                return;
            }
            var msg = $"The output {(isDir ? "directory" : "file")} '{path}' already exists.";

            if (!forceOverwrite) {
                if (Console.IsOutputRedirected) {
                    Error($"{msg} Use -y to force overwrite.");
                }
                Console.Write($"{msg} Overwrite? [y/n] ");
                string res = Console.ReadLine();
                if (!res.StartsWithIgnoreCase("y")) {
                    Error("Cancelled");
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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(msg);
            Console.ResetColor();

            Environment.Exit(1);
        }
    }
}
