using AnvilPacker.Data;
using NLog;

namespace AnvilPacker.Level.Versions
{
    internal static class NbtExtensions
    {
        public static T Pop<T>(this CompoundTag tag, string name)
        {
            T value = tag.Get<T>(name, TagGetMode.Throw);
            tag.Remove(name);
            return value;
        }
        public static T PopMaybe<T>(this CompoundTag tag, string name)
        {
            if (tag.TryGet(name, out T value)) {
                tag.Remove(name);
                return value;
            }
            return default;
        }

        public static void CopyOpaque(CompoundTag dest, CompoundTag opaque)
        {
            foreach (var (k, v) in opaque) {
                if (dest.ContainsKey(k)) {
                    //TODO: recursive merging?
                    LogManager.GetCurrentClassLogger().Warn($"Opaque tag '{k}' already exists in serialized chunk, keeping existing one.");
                    continue;
                }
                dest.Set(k, v);
            }
        }
    }
}