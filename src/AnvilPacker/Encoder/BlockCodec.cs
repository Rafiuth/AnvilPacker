#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder
{
    /// <summary> The base class for a block data codec. </summary>
    public abstract class BlockCodec
    {
        public RegionBuffer Region { get; }

        public BlockCodec(RegionBuffer region)
        {
            Region = region;
        }

        /// <summary> Encodes the blocks in the current region to the specified stream. </summary>
        public abstract void Encode(DataWriter stream, CodecProgressListener? progress = null);
        /// <summary> Decodes the blocks for the current region from the specified stream. </summary>
        public abstract void Decode(DataReader reader, CodecProgressListener? progress = null);

        /// <summary> Writes the codec header/settings to the specified stream. </summary>
        public abstract void WriteSettings(DataWriter stream);
        public abstract void ReadSettings(DataReader stream);

        public static BlockCodec CreateFromId(RegionBuffer region, int id)
        {
            var (_, klass) = KnownCodecs.FirstOrDefault(v => v.Id == id);
            Ensure.That(klass != null, $"Unknown block codec id '{id}'");

            return (BlockCodec)Activator.CreateInstance(klass, region)!;
        }
        public int GetId()
        {
            var (id, klass) = KnownCodecs.FirstOrDefault(v => v.Class == GetType());
            Ensure.That(klass != null, $"Unregistered block codec {GetType().Name}");
            return id;
        }
        public static readonly (int Id, Type Class)[] KnownCodecs = {
            (1, typeof(v1.BlockCodecV1)),
            (2, typeof(v2.BrotliBlockCodec))
        };
    }
    public class CodecProgressListener
    {
        public int TotalBlocks { get; private init; }
        public int CodedBlocks { get; private set; }
        public IProgress<double> Listener { get; private init; } = null!;

        /// <summary> Creates an instance of this class if <paramref name="listener"/> is not null. </summary>
        public static CodecProgressListener? MaybeCreate(int totalBlocks, IProgress<double>? listener)
        {
            if (listener == null) {
                return null;
            }
            return new CodecProgressListener() {
                TotalBlocks = totalBlocks,
                CodedBlocks = 0,
                Listener = listener
            };
        }

        public void Advance(int count)
        {
            CodedBlocks += count;
            if ((CodedBlocks & 16383) == 0) {
                //reporting progress is expansive, only do it every so often...
                Listener.Report(CodedBlocks / (double)TotalBlocks);
            }
        }
        public void Finish()
        {
            Debug.Assert(CodedBlocks == TotalBlocks);
            Listener.Report(1.0);
        }
    }
}