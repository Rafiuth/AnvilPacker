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
                ThrowHelper(cond, message, expr);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void ThrowHelper(bool cond, string message, string expr)
            {
                throw new InvalidOperationException(message ?? $"Assert failed: {expr}");
            }
        }

        [DebuggerStepThrough]
        public static void InRange(int value, int minInclusive, int maxInclusive, string message = null, [CallerArgumentExpression("cond")] string expr = null)
        {
            if (value < minInclusive || value > maxInclusive) {
                ThrowHelper(value, minInclusive, maxInclusive, message, expr);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void ThrowHelper(int value, int min, int max, string message, string expr)
            {
                throw new InvalidOperationException(message ?? $"Value '{value}' outside allowed ranges [{min}..{max}]");
            }
        }

        [DebuggerStepThrough]
        public static void InRange(int value1, int value2, int minInclusive, int maxInclusive, string message = null, [CallerArgumentExpression("cond")] string expr = null)
        {
            InRange(value1, minInclusive, maxInclusive, message, expr);
            InRange(value2, minInclusive, maxInclusive, message, expr);
        }
    }
}