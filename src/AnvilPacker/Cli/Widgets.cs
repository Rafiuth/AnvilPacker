using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AnvilPacker.Cli
{
    public sealed class MutableText : IRenderable
    {
        public string Text;
        public Style Style;

        public MutableText(string text = "", Style style = null)
        {
            Text = text;
            Style = style ?? Style.Plain;
        }


        public IEnumerable<Segment> Render(RenderContext context, int maxWidth)
        {
            yield return new Segment(Text, Style);
        }
        public Measurement Measure(RenderContext context, int maxWidth)
        {
            int cellCount = new Segment(Text).CellCount();

            return new Measurement(cellCount, Math.Min(cellCount, maxWidth));
        }
    }

    public class ProgressBar : IRenderable
    {
        public char FilledChar = '#';
        public Style BracketStyle = new Style(Color.Grey);
        public Style FilledStyle = new Style(Color.Green);
        public string TextFormat = "{0:0.0}%";
        public Style TextStyle = new Style(Color.White);
        public int Width = 24;

        /// <summary> Current progress value, in [0..1] range. </summary>
        public double Value = 0;

        public IEnumerable<Segment> Render(RenderContext context, int maxWidth)
        {
            double perc = Math.Clamp(Value, 0, 1);
            var text = string.Format(TextFormat, perc * 100);
            int numFilled = (int)(perc * Width + 0.5);

            int centerPos = (Width - text.Length) / 2;
            var blocks = new string(FilledChar, numFilled) +
                         new string(' ', Width - numFilled);

            var blocks1 = blocks.Substring(0, centerPos);
            var blocks2 = blocks.Substring(centerPos + text.Length);

            return new[] {
                new Segment("[", BracketStyle),
                new Segment(blocks1, FilledStyle),
                new Segment(text, TextStyle),
                new Segment(blocks2, FilledStyle),
                new Segment("]", BracketStyle)
            };
        }
        public Measurement Measure(RenderContext context, int maxWidth)
        {
            return new Measurement(4, Width + 2);
        }
    }

    public static class CliUtils
    {
        public static string FormatTime(TimeSpan time, bool isEta = false)
        {
            if (isEta && time == TimeSpan.MaxValue) {
                return "...";
            }
            if (isEta && time.TotalMinutes < 1) {
                return "< 1m";
            }
            var sb = new StringBuilder();
            if (time.TotalDays >= 1) {
                sb.AppendFormat("{0:0}d ", time.TotalDays);
            }
            if (time.TotalHours >= 1) {
                sb.AppendFormat("{0:0}h ", time.Hours);
            }
            if (time.TotalMinutes >= 1) {
                sb.AppendFormat("{0:0}m ", time.Minutes);
            }
            if (!isEta) {
                sb.AppendFormat("{0:0}s", time.Seconds);
            }
            if (sb[^1] == ' ') {
                sb.Remove(sb.Length - 1, 1);
            }
            return sb.ToString();
        }
    }
}