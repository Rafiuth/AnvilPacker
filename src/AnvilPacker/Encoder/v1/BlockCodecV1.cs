using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using AnvilPacker.Data;
using AnvilPacker.Data.Entropy;
using AnvilPacker.Level;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.v1
{
    //TODO: Pick a name for this codec
    public class BlockCodecV1 : BlockCodec
    {
        public int ContextBits = 13; //log2 number of contexts to use
        public Vec3i[] Neighbors = DefaultNeighbors; //context of a block (relative coords to previously coded blocks)

        public static readonly Vec3i[] DefaultNeighbors = {
            new(-1, 0, 0),
            new(0, -1, 0),
            new(0, 0, -1),
        };
        //Are we using the default neighbors?
        //If so, GetContext() will use an optimized keyer and get a ~1.25x speedup
        //False case doesn't seem to be much affected performance wise.
        private bool _areDefaultNeighbors;

        public BlockCodecV1(RegionBuffer region) : base(region)
        {
        }

        public override void Encode(DataWriter stream, CodecProgressListener? progress = null)
        {
            var ac = new ArithmEncoder(stream);
            var contexts = new Context[1 << ContextBits];

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(Region)) {
                EncodeBlocks(chunk, y, contexts, ac);

                progress?.Advance(256);
            }
            ac.Flush();

            progress?.Finish();
        }

        public override void Decode(DataReader stream, CodecProgressListener? progress = null)
        {
            var ac = new ArithmDecoder(stream);
            var contexts = new Context[1 << ContextBits];

            foreach (var (chunk, y) in ChunkIterator.CreateLayered(Region)) {
                DecodeBlocks(chunk, y, contexts, ac);

                progress?.Advance(256);
            }
            progress?.Finish();
        }

        private void EncodeBlocks(ChunkIterator chunk, int y, Span<Context> contexts, ArithmEncoder ac)
        {
            Debug.Assert(chunk.Palette == Region.Palette);

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    var ctx = GetContext(chunk, x, y, z,  contexts);
                    var id = chunk.GetBlockIdFast(x, y, z);
                    ctx.Write(ac, id);
                }
            }
        }

        private void DecodeBlocks(ChunkIterator chunk, int y, Span<Context> contexts, ArithmDecoder ac)
        {
            Debug.Assert(chunk.Palette == Region.Palette);

            for (int z = 0; z < 16; z++) {
                for (int x = 0; x < 16; x++) {
                    var ctx = GetContext(chunk, x, y, z, contexts);
                    var id = ctx.Read(ac);
                    chunk.SetBlockId(x, y, z, id);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Context GetContext(ChunkIterator chunk, int x, int y, int z, Span<Context> contexts)
        {
            ulong key;

            if (_areDefaultNeighbors) {
                key = (ulong)chunk.GetBlockId(x - 1, y, z) << 32 |
                      (ulong)chunk.GetBlockId(x, y - 1, z) << 16 |
                      (ulong)chunk.GetBlockId(x, y, z - 1) << 0;
            } else {
                key = 0;
                foreach (var pos in Neighbors) {
                    int nx = x + pos.X;
                    int ny = y + pos.Y;
                    int nz = z + pos.Z;

                    key = (key << 16) | chunk.GetBlockId(nx, ny, nz);
                }
            }
            int slot = Context.GetSlot(key, ContextBits);
            return contexts[slot] ??= new Context(Region.Palette);
        }

        public override void WriteHeader(DataWriter stream)
        {
            ValidateSettings();
            
            stream.WriteByte(0); //version

            stream.WriteByte(ContextBits);
            stream.WriteByte(Neighbors.Length);
            foreach (var (nx, ny, nz) in Neighbors) {
                Ensure.That(ny <= 0 && (nx <= 0 || nz <= 0), "Neighbor coord must point to a block that was previously encoded."); //ensure neighbor was decoded before
                
                stream.WriteSByte(nx);
                stream.WriteSByte(ny);
                stream.WriteSByte(nz);
            }
            _areDefaultNeighbors = Neighbors.SequenceEqual(DefaultNeighbors);
        }
        public override void ReadHeader(DataReader stream)
        {
            Ensure.That(stream.ReadByte() == 0, "Unsupported codec version");

            ContextBits = stream.ReadByte();
            Neighbors = new Vec3i[stream.ReadByte()];
            ValidateSettings();

            for (int i = 0; i < Neighbors.Length; i++) {
                Neighbors[i] = new Vec3i(
                    stream.ReadSByte(),
                    stream.ReadSByte(),
                    stream.ReadSByte()
                );
            }
            _areDefaultNeighbors = Neighbors.SequenceEqual(DefaultNeighbors);
        }

        private void ValidateSettings()
        {
            Ensure.That(ContextBits is > 0 and <= 16, "ContextBits must be between 1 and 16.");
            Ensure.That(Neighbors.Length <= 4, "Neighbors must have at most 4 elements.");
        }
    }
}