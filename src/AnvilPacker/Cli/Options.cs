using System;
using System.Collections.Generic;
using CommandLine;
using NLog;

namespace AnvilPacker.Cli
{
    public abstract class CliOptions
    {
        [Option('T', "threads", HelpText = "Number of threads to use during processing. Higher values demands more memory, as one region is processed per thread.")]
        public int MaxThreads { get; set; } = Environment.ProcessorCount;

        [Option("log-level", HelpText = "Sets the log level. trace|debug|info|warn|error|fatal")]
        public string LogLevel { get; set; } = "info";

        [Option("log-file", HelpText = "Sets the path of the log file.")]
        public string? LogFile { get; set; }
    }
    public class IOPathOptions : CliOptions
    {
        [Option('i', "input", Required = true, HelpText = "Path of the input file/directory.")]
        public string InputPath { get; set; } = null!;

        [Option('o', "output", Required = true, HelpText = "Path of the resulting file.")]
        public string OutputPath { get; set; } = null!;

        [Option('y', "overwrite", HelpText = "Overwrite the output file if it already exists.")]
        public bool Overwrite { get; set; } = false;
    }

    [Verb("pack", HelpText = "Compresses a given world.")]
    public class PackOptions : IOPathOptions
    {
        [Option("preset", HelpText = "Use predefined settings.")]
        public string? Preset { get; set; }

        [Option("transform-pipe", HelpText = "A list of transforms to apply in regions.")]
        public string? TransformPipe { get; set; }

        [Option("add-transforms", HelpText = "Additional transforms for the specified preset.")]
        public string? AddTransforms { get; set; }

        [Option('e', "encoder-opts", HelpText = "Sets the region encoder options.")]
        public string? EncoderOpts { get; set; }

        [Option("no-blobs", HelpText = "Disable solid compression of small files.")]
        public bool NoBlobs { get; set; } = false;
    }

    [Verb("unpack", HelpText = "Decompresses a given world.")]
    public class UnpackOptions : IOPathOptions
    {
        [Option("dont-lit", HelpText = "Don't precompute light data for chunks targeting version >= 1.14.4. Only affects chunks whose light was stripped.")]
        public bool DontLit { get; set; } = false;
    }

    [Verb("dump", HelpText = "Generates debug files for a given world.")]
    public class DumpOptions : IOPathOptions
    {
        [Option('t', "type", Required = true, HelpText = "Specifies what kind of data to dump.")]
        public DumpType Type { get; set; }

        [Option("exclude-blocks", HelpText = "Exclude block data from NBT dumps.")]
        public bool ExcludeBlocks { get; set; }
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
