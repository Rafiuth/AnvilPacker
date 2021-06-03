using System;
using System.IO;
using System.Threading;

namespace AnvilPacker.Util
{
    public static class Utils
    {
        public static double InterlockedAdd(ref double location1, double value)
        {
            //https://stackoverflow.com/a/16893641
            double newCurrentValue = Volatile.Read(ref location1);
            while (true) {
                double currentValue = newCurrentValue;
                double newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue == currentValue) {
                    return newValue;
                }
            }
        }

        /// <summary> Checks if the file path has the specified extension. </summary>
        /// <param name="extension">The extension to check for; may start with a period. </param>
        public static bool FileHasExtension(string path, string extension)
        {
            var actualExt = Path.GetExtension(path.AsSpan());
            if (extension[0] != '.' && actualExt.Length > 0) {
                actualExt = actualExt[1..];
            }
            return actualExt.Equals(extension, StringComparison.OrdinalIgnoreCase);
        }
        public static bool FileHasExtension(string path, params string[] extensions)
        {
            var actualExt = Path.GetExtension(path.AsSpan());
            foreach (var ext in extensions) {
                var actualExtTrimmed = actualExt;
                if (ext[0] != '.' && actualExtTrimmed.Length > 0) {
                    actualExtTrimmed = actualExtTrimmed[1..];
                }
                if (actualExtTrimmed.Equals(ext, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }
    }
}