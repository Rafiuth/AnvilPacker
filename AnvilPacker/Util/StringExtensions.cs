using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    public static class StringExtensions
    {
        public static bool EqualsIgnoreCase(this string a, string b)
        {
            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }
        public static bool EqualsAnyIgnoreCase(this string a, params string[] opts)
        {
            foreach (var s in opts) {
                if (a.Equals(s, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public static bool StartsWithIgnoreCase(this string a, string b)
        {
            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase);
        }
        public static bool StartsWithAny(this string a, params string[] opts)
        {
            foreach (var s in opts) {
                if (a.StartsWith(s)) {
                    return true;
                }
            }
            return false;
        }
        public static bool StartsWithAnyIgnoreCase(this string a, params string[] opts)
        {
            foreach (var s in opts) {
                if (a.StartsWith(s, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public static bool EndsWithIgnoreCase(this string a, string b)
        {
            return a.EndsWith(b, StringComparison.OrdinalIgnoreCase);
        }
        public static bool EndsWithAny(this string a, params string[] b)
        {
            foreach (var s in b) {
                if (a.EndsWith(s)) {
                    return true;
                }
            }
            return false;
        }
        public static bool EndsWithAnyIgnoreCase(this string a, params string[] opts)
        {
            foreach (var s in opts) {
                if (a.EndsWith(s, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }
    }
}
