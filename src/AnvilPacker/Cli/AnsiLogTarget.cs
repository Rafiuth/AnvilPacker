using System.Collections.Concurrent;
using NLog;
using NLog.Common;
using Spectre.Console;

namespace AnvilPacker.Cli
{
    public class AnsiLogTarget : NLog.Targets.Target
    {
        //Workaround for https://github.com/spectreconsole/spectre.console/issues/435
        //It also reduces flickering so might as well keep it...
        private static ConcurrentQueue<Paragraph> _queue = new();
        public static volatile bool BufferLogs = false;

        protected override void Write(LogEventInfo e)
        {
            var p = new Paragraph();

            p.Append($"[{e.Level.Name.ToUpper()}] ", new Style(GetLevelColor(e.Level)));
            p.Append(e.FormattedMessage);

            if (BufferLogs) {
                _queue.Enqueue(p);
            } else {
                p.Append("\n");
                AnsiConsole.Render(p);
            }
        }
        
        public static void FlushBuffer()
        {
            //Rendering each line individualy is pretty slow when a live display active.
            var grid = new Grid();
            grid.AddColumn();
            while (_queue.TryDequeue(out var log)) {
                grid.AddRow(log);
            }
            if (grid.Rows.Count > 0) {
                AnsiConsole.Render(grid);
            }
        }

        private Color? GetLevelColor(LogLevel level)
        {
            return level.Ordinal switch {
                0 /* Trace */ => Color.Grey,
                1 /* Debug */ => Color.Silver,
                2 /* Info  */ => Color.White,
                3 /* Warn  */ => Color.Yellow,
                4 /* Error */ => Color.Red,
                5 /* Fatal */ => Color.DarkRed,
                _ => Color.Grey
            };
        }
    }
}