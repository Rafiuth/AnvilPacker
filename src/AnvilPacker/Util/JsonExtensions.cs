using System.Text.Json;

namespace AnvilPacker.Util
{
    public static class JsonExtensions
    {
        public static string GetString(this in JsonElement je, string key)
        {
            return je.GetProperty(key).GetString() ?? throw new NullReferenceException();
        }
        public static int GetInt(this in JsonElement je, string key)
        {
            return je.GetProperty(key).GetInt32();
        }
        public static long GetLong(this in JsonElement je, string key)
        {
            return je.GetProperty(key).GetInt64();
        }
        public static double GetDouble(this in JsonElement je, string key)
        {
            return je.GetProperty(key).GetDouble();
        }

        public static int GetInt(this in JsonElement je) => je.GetInt32();
        public static long GetLong(this in JsonElement je) => je.GetInt64();
    }
}