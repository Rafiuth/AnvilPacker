using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Util
{
    public static class Ensure
    {
        [DebuggerStepThrough]
        public static void That([DoesNotReturnIf(false)] bool cond, string message = null, [CallerArgumentExpression("cond")] string expr = null)
        {
            if (!cond) {
                ThrowHelper(message, expr);
            }

            static void ThrowHelper(string message, string expr)
            {
                throw new InvalidOperationException(message ?? $"Assert failed: {expr}");
            }
        }

        [DebuggerStepThrough]
        public static void InRange(int value, int minInclusive, int maxInclusive, string message = null)
        {
            if (value < minInclusive || value > maxInclusive) {
                ThrowHelper(value, minInclusive, maxInclusive, message);
            }

            static void ThrowHelper(int value, int min, int max, string message)
            {
                throw new InvalidOperationException(message ?? $"Value '{value}' is outside allowed range [{min}..{max}]");
            }
        }

        [DebuggerStepThrough]
        public static void InRange(int value1, int value2, int minInclusive, int maxInclusive, string message = null)
        {
            InRange(value1, minInclusive, maxInclusive, message);
            InRange(value2, minInclusive, maxInclusive, message);
        }

        [DebuggerStepThrough]
        public static void RangeValid(int offset, int count, int maxCount, string message = null)
        {
            //https://github.com/dotnet/runtime/blob/8194ffb2c0973cb1ec549a3c9f0633d0e3d6acad/src/libraries/System.Private.CoreLib/src/System/Span.cs#L407
            if ((uint)(ulong)offset + (uint)(ulong)count >= (ulong)(uint)maxCount) {
                ThrowHelper(offset, count, maxCount, message);
            }

            static void ThrowHelper(int offset, int count, int maxCount, string message)
            {
                throw new IndexOutOfRangeException(message ?? $"Range 'offset={offset} count={count}' outside collection bounds '{maxCount}'");
            }
        }

        [DebuggerStepThrough]
        public static void IndexValid(int index, int length, string message = null)
        {
            if ((uint)index >= (uint)length) {
                ThrowHelper(index, length, message);
            }

            static void ThrowHelper(int index, int length, string message)
            {
                throw new IndexOutOfRangeException(message ?? $"Index {index} outside collection bounds {length}");
            }
        }
    }
}