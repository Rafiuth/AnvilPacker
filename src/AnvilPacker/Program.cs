using System;
using System.IO;
using System.Linq;
using AnvilPacker.Cli;
using AnvilPacker.Container;
using AnvilPacker.Level;
using AnvilPacker.Util;
using CommandLine;
using CommandLine.Text;
using NLog;

namespace AnvilPacker
{
    public partial class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            var programs = new (Type OptsType, Action<CliOptions> Run)[] {
                (typeof(PackOptions),       opts => RunPacker((PackOptions)opts)),
                (typeof(UnpackOptions),     opts => RunUnpacker((UnpackOptions)opts)),
                (typeof(DumpOptions),       opts => RunDumper((DumpOptions)opts)),
            };

            var parser = new Parser(s => {
                s.CaseInsensitiveEnumValues = true;
                s.HelpWriter = null;
            });
            var result = parser.ParseArguments(
                args, 
                programs.Select(p => p.OptsType).ToArray()
            );
            if (result is Parsed<object> parsed) {
                var opts = (CliOptions)parsed.Value;
                var program = programs.First(v => v.OptsType == opts.GetType());

                Init(opts);
                program.Run(opts);
            } else {
                var text = HelpText.AutoBuild(
                    result,
                    err => {
                        err.Heading = "AnvilPacker v" + PackProcessor.GetInfoVersion();
                        err.AdditionalNewLineAfterOption = false;
                        err.Copyright = "";
                        return err;
                    }
                );
                Console.WriteLine(text);
            }
        }

        private static void Init(CliOptions? opts)
        {
            var config = new NLog.Config.LoggingConfiguration();
            var minLogLevel = opts.LogLevel != null ? LogLevel.FromString(opts.LogLevel) :
#if DEBUG
            LogLevel.Debug;
#else
            LogLevel.Info; 
#endif
            config.AddRule(minLogLevel, LogLevel.Fatal, BufferedConsoleLogTarget.Instance);
            if (opts.LogFile != null) {
                var fileTarget = new NLog.Targets.FileTarget();
                fileTarget.ArchiveOldFileOnStartup = false;
                fileTarget.FileName = opts.LogFile;
                fileTarget.Layout = "[${level:uppercase=true} ${logger:shortName=true}] ${replace-newlines:replacement=\n:${message}} ${exception:format=toString}";
                config.AddRule(minLogLevel, LogLevel.Fatal, fileTarget);
            }
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
