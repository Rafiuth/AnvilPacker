using System;
using System.Collections.Concurrent;
using System.Text;
using NLog;
using NLog.Common;

namespace AnvilPacker.Cli
{
    public class BufferedConsoleLogTarget : NLog.Targets.Target
    {
        private static ConcurrentQueue<LogEventInfo> _queue = new();
        public static volatile bool EnableBuffer = false;

        public static BufferedConsoleLogTarget Instance { get; } = new();

        protected override void Write(LogEventInfo evt)
        {
            _queue.Enqueue(evt);

            if (!EnableBuffer) {
                Flush();
            }
        }
        
        public static void Flush()
        {
            var sb = new StringBuilder();
            var color = ConsoleColor.Black;

            while (_queue.TryDequeue(out var evt)) {
                var eventColor = GetLevelColor(evt.Level);
                if (eventColor != color) {
                    Console.Write(sb);
                    sb.Clear();
                    Console.ForegroundColor = eventColor;
                    color = eventColor;
                }
                sb.AppendFormat("[{0}] {1}{2}{3}\n", evt.Level.ToString().ToUpper(), evt.FormattedMessage, evt.Exception == null ? "" : "\n", evt.Exception);
            }
            Console.Write(sb);
            Console.ResetColor();
        }

        private static ConsoleColor GetLevelColor(LogLevel level)
        {
            return level.Ordinal switch {
                0 /* Trace */ => ConsoleColor.DarkGray,
                1 /* Debug */ => ConsoleColor.Gray,
                2 /* Info  */ => ConsoleColor.White,
                3 /* Warn  */ => ConsoleColor.Yellow,
                4 /* Error */ => ConsoleColor.Red,
                5 /* Fatal */ => ConsoleColor.DarkRed,
                _   => ConsoleColor.Gray
            };
        }
    }
}