using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        /// <param name="extension">The extension to check for; must start with a period. </param>
        public static bool FileHasExtension(string path, string extension)
        {
            var actualExt = Path.GetExtension(path.AsSpan());
            return actualExt.Equals(extension, StringComparison.OrdinalIgnoreCase);
        }
        /// <summary> Checks if the file path has any of the specified extensions. </summary>
        /// <param name="extension">The array with the extensions to check for; they all must start with a period. </param>
        public static bool FileHasExtension(string path, params string[] extensions)
        {
            var actualExt = Path.GetExtension(path.AsSpan());
            foreach (var ext in extensions) {
                if (actualExt.Equals(ext, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary> Tests if <paramref name="subPath"/> is inside <paramref name="basePath"/></summary>
        public static bool IsSubPath(string basePath, string subPath)
        {
            //https://stackoverflow.com/a/66877016
            var rel = Path.GetRelativePath(basePath, subPath);
            return !rel.StartsWith('.') && !Path.IsPathRooted(rel);
        }

        private static readonly Regex INVALID_FILE_CHARS_REGEX =
            new Regex(
                "[" +
                string.Join('|',
                    Path.GetInvalidFileNameChars()
                        .Select(c => Regex.Escape(c.ToString()))
                )
                + "]"
            );

        public static string RemoveInvalidPathChars(string name)
        {
            return INVALID_FILE_CHARS_REGEX.Replace(name, "_");
        }
    }
}