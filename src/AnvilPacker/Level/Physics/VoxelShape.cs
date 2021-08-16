using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using AnvilPacker.Util;

namespace AnvilPacker.Level.Physics
{
    public unsafe class VoxelShape : IEquatable<VoxelShape>
    {
        public static VoxelShape Empty { get; } = new(new Box8[0]);
        public static VoxelShape Cube { get; } = new(new Box8[] { new(0, 0, 0, 16, 16, 16) });

        public Box8[] Boxes { get; }

        /// <summary> Creates a new voxel shape backed by the specified array. (not copied) </summary>
        public VoxelShape(Box8[] boxes)
        {
            Boxes = boxes;
        }

        public static unsafe bool MergedFacesOccludes(VoxelShape a, VoxelShape b, Direction dir)
        {
            //return Intersect(Face(FullCube, dir), Union(Face(a, dir), Face(b, opposite(dir)))) == Empty
            //This rasterization method is probably faster than sweep-plane, since it's only 16x16 pixels and it can be easily vectorized.
            //(Minecraft seems to use a similar approach btw)
            //https://stackoverflow.com/questions/50656051/multiple-bounding-boxes-containment-detection-algorithm

            var bmp = stackalloc ushort[16];
            Unsafe.InitBlock(bmp, 0, 16);
            
            Rasterize(a, bmp, dir);
            Rasterize(b, bmp, dir.Opposite());

            return AllBitsSet256(bmp);

            static void Rasterize(VoxelShape shape, ushort* bmp, Direction dir)
            {
                foreach (var box in shape.Boxes) {
                    //check if the box touches the face
                    var axis = dir.Axis();
                    if (dir.AnyNeg() ? box.Min(axis) > 0 : box.Max(axis) < 16) {
                        continue;
                    }
                    RasterizePlane(box, bmp, axis);
                }
            }
        }

        /// <summary> Checks whether the shape completely covers the specified axis. </summary>
        public static bool FullyOccludesAxis(VoxelShape shape, Axis axis)
        {
            var bmp = stackalloc ushort[16];
            Unsafe.InitBlock(bmp, 0, 16);

            foreach (var box in shape.Boxes) {
                RasterizePlane(box, bmp, axis);
            }
            return AllBitsSet256(bmp);
        }
        
        /// <summary> Rasterizes the specified axis plane of the box to a 16x16 bitmap. </summary>
        private static void RasterizePlane(Box8 box, ushort* bmp, Axis axis)
        {
            //pick plane coords
            var (x1, y1, x2, y2) = axis switch {
                Axis.X => (box.MinZ, box.MinY, box.MaxZ, box.MaxY),
                Axis.Y => (box.MinX, box.MinZ, box.MaxX, box.MaxZ),
                Axis.Z => (box.MinX, box.MinY, box.MaxX, box.MaxY),
                _ => throw new InvalidOperationException()
            };
            x1 = Clamp16(x1);
            y1 = Clamp16(y1);
            x2 = Clamp16(x2);
            y2 = Clamp16(y2);

            //draw to the bitmap
            var rowMask = (ushort)Maths.CreateRangeMask(x1, x2);
            if (Avx2.IsSupported) {
                Rasterize_AVX2(bmp, y1, y2, rowMask);
            } else {
                Rasterize_Scalar(bmp, y1, y2, rowMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void Rasterize_Scalar(ushort* bmp, int y1, int y2, ushort rowMask)
            {
                while (y2 - y1 >= 4) {
                    *(ulong*)&bmp[y1] |= (ulong)rowMask * 0x0001_0001_0001_0001ul;
                    y1 += 4;
                }
                while (y1 < y2) {
                    bmp[y1++] |= rowMask;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void Rasterize_AVX2(ushort* bmp, int y1, int y2, ushort rowMask)
            {
                //colMask = set ~0 words in range [y1..y2]
                //&bmp[0] |= broadcast((ushort)rowMask) & colMask;
                var colIdx = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
                var colMask = Avx2.AndNot(
                    Avx2.CompareGreaterThan(Vector256.Create((short)y1), colIdx),
                    Avx2.CompareGreaterThan(Vector256.Create((short)y2), colIdx)
                ).AsUInt16();

                var col = Avx2.LoadVector256(&bmp[0]);
                var row = Vector256.Create(rowMask);
                col = Avx2.Or(col, Avx2.And(row, colMask));
                Avx2.Store(&bmp[0], col);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static sbyte Clamp16(sbyte x)
            {
                return (sbyte)(x < 0 ? 0 : x > 16 ? 16 : x);
            }
        }

        private static bool AllBitsSet256(ushort* bmp)
        {
            if (Avx.IsSupported) {
                var v = Avx.LoadVector256(bmp);
                return Avx.TestC(v, Vector256<ushort>.AllBitsSet); //(~v & ~0) == 0
            }
            var bmp64 = (ulong*)bmp;
            return (bmp64[0] & bmp64[1] & bmp64[2] & bmp64[3]) == ~0ul;
        }

        public bool Equals(VoxelShape? other)
        {
            return other != null && Boxes.AsSpan().SequenceEqual(other.Boxes);
        }
        public override bool Equals(object? obj)
        {
            return obj is VoxelShape other && Equals(other);
        }
        public override int GetHashCode()
        {
            return Boxes.CombinedHashCode();
        }

        public override string ToString()
        {
            return $"VoxelShape({string.Join(", ", Boxes)})";
        }
    }
}