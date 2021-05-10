using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Util
{
    public static class Ensure
    {
        [DebuggerStepThrough]
        public static void That(bool cond, string message = null, [CallerArgumentExpression("cond")] string expr = null)
        {
            if (!cond) {
                throw new InvalidOperationException(message ?? $"Assert failed: {expr}");
            }
        }
    }
}