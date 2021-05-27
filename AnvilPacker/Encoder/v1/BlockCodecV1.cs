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
    // Fixed Order Context + CABAC based
    public class BlockCodecV1 : BlockCodec
    {
        public int ContextBits = 13; //log2 number of contexts to use
        public Vec3i[] ContextNeighbors = { //context of a block - each coord is relative and points to an already decoded block
            new(-1, 0, 0),
            new(0, -1, 0),
            new(0, 0, -1),
        };

        public BlockCodecV1(RegionBuffer region) : base(region)
        {
        }

        public override void Encode(DataWriter stream, CodecProgressListener progress = null)
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

        public override void Decode(DataReader stream, CodecProgressListener progress = null)
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
            ulong key = 0;

            foreach (var pos in ContextNeighbors) {
                int nx = x + pos.X;
                int ny = y + pos.Y;
                int nz = z + pos.Z;

                key = (key << 16) | chunk.GetBlockId(nx, ny, nz);
            }
            int slot = Context.GetSlot(key, ContextBits);
            return contexts[slot] ??= new Context(Region.Palette);
        }

        public override void WriteSettings(DataWriter stream)
        {
            stream.WriteByte(0); //version

            stream.WriteByte(ContextBits);
            stream.WriteByte(ContextNeighbors.Length);
            foreach (var (nx, ny, nz) in ContextNeighbors) {
                Ensure.That(ny <= 0 && (nx <= 0 || nz <= 0)); //ensure neighbor was decoded before
                
                stream.WriteSByte(nx);
                stream.WriteSByte(ny);
                stream.WriteSByte(nz);
            }
        }
        public override void ReadSettings(DataReader stream)
        {
            Ensure.That(stream.ReadByte() == 0, "Unsupported codec version");

            ContextBits = stream.ReadByte();
            ContextNeighbors = new Vec3i[stream.ReadByte()];
            Ensure.That(ContextNeighbors.Length <= 4, "Can only have at most 4 context neighbors.");

            for (int i = 0; i < ContextNeighbors.Length; i++) {
                ContextNeighbors[i] = new Vec3i(
                    stream.ReadSByte(),
                    stream.ReadSByte(),
                    stream.ReadSByte()
                );
            }
        }
    }
}