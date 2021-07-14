using System;
using System.Collections.Generic;
using CommandLine;
using NLog;

namespace AnvilPacker.Cli
{
    public abstract class CliOptions
    {
        [Option('i', "input", Required = true, HelpText = "Path of the input file/directory.")]
        public string Input { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path of the resulting file.")]
        public string Output { get; set; }

        [Option('y', "overwrite", HelpText = "Overwrite the output file if it already exists.")]
        public bool Overwrite { get; set; } = false;

        [Option('T', "threads", HelpText = "Number of threads to use during processing. Higher values demands more memory, as one region is processed per thread.")]
        public int MaxThreads { get; set; } = Environment.ProcessorCount;

        [Option("log-level", HelpText = "Sets the log level. trace/debug/info/warn/error/fatal")]
        public string LogLevel { get; set; } = "info";

        [Option("log-file", HelpText = "Sets the path of the log file.")]
        public string LogFile { get; set; }
    }
    [Verb("pack", HelpText = "Compresses a given world.")]
    public class PackOptions : CliOptions
    {
        [Option("preset", HelpText = "Use predefined settings.")]
        public string Preset { get; set; }

        [Option("transform-pipe", HelpText = "A list of transforms to apply in regions.")]
        public string TransformPipe { get; set; }

        [Option("encoder-opts", HelpText = "Sets the region encoder options.")]
        public string EncoderOpts { get; set; }
    }

    [Verb("unpack", HelpText = "Decompresses a given world.")]
    public class UnpackOptions : CliOptions
    {
    }

    [Verb("dump", HelpText = "Generates debug files for a given world.")]
    public class DumpOptions : CliOptions
    {
        [Option('t', "type", Required = true, HelpText = "Sets the output data type.")]
        public DumpType Type { get; set; }
    }
    public enum DumpType
    {
        //Creates a file containing all blocks in a region. 1 byte per block if PaletteLen is <= 256, 2 otherwise. 
        //Blocks can be indexed using `(y * 512 + z) * 512 + x`
        RawBlocks,
        //Similar to RawBlocks, except that this groups blocks into 16x16x16 chunks.
        //Empty chunks are skipped. Intended for benchmarking purposes.
        RawChunks,
        //Creates a file with all tags in a region merged into a single list tag.
        Nbt,
        //Pretty prints a nbt/region file.
        NbtPrint
    }
}
