using AnvilPacker.Data;

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
        public static T MaybePop<T>(this CompoundTag tag, string name)
        {
            if (tag.TryGet(name, out T value)) {
                tag.Remove(name);
                return value;
            }
            return default;
        }
    }
}