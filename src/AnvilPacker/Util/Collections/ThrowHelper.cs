// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AnvilPacker.Util.Collections
{
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFoundException<T>(T key)
        {
            throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
        }
        internal static void ThrowDuplicatedKeyException<T>(T key)
        {
            throw new InvalidOperationException($"An item with the same key has already been added. Key: {key}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyArgumentNullException()
        {
            throw new ArgumentNullException("key");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCapacityArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException("capacity");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool ThrowNotSupportedException()
        {
            throw new NotSupportedException();
        }
    }
}