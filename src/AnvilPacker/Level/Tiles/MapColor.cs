using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level
{
    public enum MapColor : byte
    {
        Air = 0,
        Grass = 1,
        Sand = 2,
        Wool = 3,
        Fire = 4,
        Ice = 5,
        Metal = 6,
        Plant = 7,
        Snow = 8,
        Clay = 9,
        Dirt = 10,
        Stone = 11,
        Water = 12,
        Wood = 13,
        Quartz = 14,
        ColorOrange = 15,
        ColorMagenta = 16,
        ColorLightBlue = 17,
        ColorYellow = 18,
        ColorLightGreen = 19,
        ColorPink = 20,
        ColorGray = 21,
        ColorLightGray = 22,
        ColorCyan = 23,
        ColorPurple = 24,
        ColorBlue = 25,
        ColorBrown = 26,
        ColorGreen = 27,
        ColorRed = 28,
        ColorBlack = 29,
        Gold = 30,
        Diamond = 31,
        Lapis = 32,
        Emerald = 33,
        Podzol = 34,
        Nether = 35,
        TerracottaWhite = 36,
        TerracottaOrange = 37,
        TerracottaMagenta = 38,
        TerracottaLightBlue = 39,
        TerracottaYellow = 40,
        TerracottaLightGreen = 41,
        TerracottaPink = 42,
        TerracottaGray = 43,
        TerracottaLightGray = 44,
        TerracottaCyan = 45,
        TerracottaPurple = 46,
        TerracottaBlue = 47,
        TerracottaBrown = 48,
        TerracottaGreen = 49,
        TerracottaRed = 50,
        TerracottaBlack = 51,
        CrimsonNylium = 52,
        CrimsonStem = 53,
        CrimsonHyphae = 54,
        WarpedNylium = 55,
        WarpedStem = 56,
        WarpedHyphae = 57,
        WarpedWartBlock = 58,
        Deepslate = 59,
        RawIron = 60,
        GlowLichen = 61
    }
    public static class MapColors
    {
        public static readonly int[] Colors = {
            0x000000, 0x7FB238, 0xF7E9A3, 0xC7C7C7, 0xFF0000, 0xA0A0FF, 0xA7A7A7, 0x007C00,
            0xFFFFFF, 0xA4A8B8, 0x976D4D, 0x707070, 0x4040FF, 0x8F7748, 0xFFFCF5, 0xD87F33,
            0xB24CD8, 0x6699D8, 0xE5E533, 0x7FCC19, 0xF27FA5, 0x4C4C4C, 0x999999, 0x4C7F99,
            0x7F3FB2, 0x334CB2, 0x664C33, 0x667F33, 0x993333, 0x191919, 0xFAEE4D, 0x5CDBD5,
            0x4A80FF, 0x00D93A, 0x815631, 0x700200, 0xD1B1A1, 0x9F5224, 0x95576C, 0x706C8A,
            0xBA8524, 0x677535, 0xA04D4E, 0x392923, 0x876B62, 0x575C5C, 0x7A4958, 0x4C3E5C,
            0x4C3223, 0x4C522A, 0x8E3C2E, 0x251610, 0xBD3031, 0x943F61, 0x5C191D, 0x167E86,
            0x3A8E8C, 0x562C3E, 0x14B485, 0x646464, 0xD8AF93, 0x7FA796
        };
        private static readonly int[] ShadeBrightness = { 180, 220, 255, 135 };

        public static int ToRgb(this MapColor color, int shade = 2)
        {
            return ToRgb((int)color, shade);
        }

        public static int ToRgb(int id, int shade = 2)
        {
            var color = Colors[id];
            int br = ShadeBrightness[shade];

            int r = ((color >> 16) & 0xFF) * br / 255;
            int g = ((color >>  8) & 0xFF) * br / 255;
            int b = ((color >>  0) & 0xFF) * br / 255;
            return 255 << 24 | r << 16 | g << 8 | b << 0;
        }
        public static int ToRgb(byte id)
        {
            //int idx = x + y * 128;
            //shade = id & 3;
            //color = id >> 2;
            //if (color == 0)
            //    actual = (idx + idx / 128 & 1) * 8 + 16 << 24;
            //  ~ actual = [0x10_000000, 0x18_000000][(x + y) & 1]
            //else
            //    actual = GetShadedColor(color, shade);

            return ToRgb(id >> 2, id & 3);
        }
    }
}
