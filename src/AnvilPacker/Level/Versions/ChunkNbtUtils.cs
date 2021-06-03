using System;
using AnvilPacker.Data;
using NLog;

namespace AnvilPacker.Level.Versions
{
    internal static class ChunkNbtUtils
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

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

        public static void MergeOpaque(NbtTag src, NbtTag dst)
        {
            if (src.Type != dst.Type) {
                _logger.Warn("OpaqueMerger: tag types differ: src={0} dst={0}", src.Type, dst.Type);
                return;
            }
            switch (src.Type) {
                case TagType.Compound: {
                    MergeCompound((CompoundTag)src, (CompoundTag)dst);
                    break;
                }
                default: {
                    _logger.Warn("OpaqueMerger: Don't know how to merge {0} tags.", src.Type);
                    break;
                }
            }
        }

        private static void MergeCompound(CompoundTag src, CompoundTag dst)
        {
            foreach (var (name, tag) in src) {
                if (name == "Sections") {
                    MergeSections((ListTag)tag, dst.GetList(name));
                    continue;
                }
                if (tag.Type == TagType.Compound) {
                    var dstTag = dst.GetCompound(name, TagGetMode.Create);
                    MergeCompound((CompoundTag)tag, dstTag);
                    continue;
                }
                if (dst.ContainsKey(name)) {
                    _logger.Warn("OpaqueMerger: dest already contains {0} of type {1}. Ignoring.", name, tag.Type);
                    continue;
                }
                dst.Set(name, tag);
            }
        }
        private static void MergeSections(ListTag src, ListTag dst)
        {
            foreach (CompoundTag srcSection in src) {
                CompoundTag dstSection = FindSection(dst, srcSection.GetByte("Y"));
                if (dstSection == null) {
                    dst.Add(srcSection);
                } else {
                    MergeCompound(srcSection, dstSection);
                }
            }
        }
        private static CompoundTag FindSection(ListTag list, int y)
        {
            foreach (CompoundTag sect in list) {
                if (sect.GetByte("Y") == y) {
                    return sect;
                }
            }
            return null;
        }
    }
}