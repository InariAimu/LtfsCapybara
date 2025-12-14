using System;

namespace Ltfs
{
    // Console logger implementation moved into the Ltfs project so any
    // consumer can opt-in without referencing the Test project.
    public class ConsoleLogger : ILtfsLogger
    {
        public LogLevel Level { get; set; } = LogLevel.Info;

        private static void GetShortLevelAndColor(LogLevel level, out char shortLevel, out ConsoleColor color)
        {
            switch (level)
            {
                case LogLevel.Error:
                    shortLevel = 'E';
                    color = ConsoleColor.Red;
                    break;
                case LogLevel.Warn:
                    shortLevel = 'W';
                    color = ConsoleColor.Yellow;
                    break;
                case LogLevel.Info:
                    shortLevel = 'I';
                    color = ConsoleColor.Cyan;
                    break;
                case LogLevel.Debug:
                    shortLevel = 'D';
                    color = ConsoleColor.Green;
                    break;
                case LogLevel.Trace:
                    shortLevel = 'V';
                    color = ConsoleColor.DarkGray;
                    break;
                default:
                    shortLevel = '?';
                    color = Console.ForegroundColor;
                    break;
            }
        }

        private void Write(LogLevel level, string message)
        {
            if (Level == LogLevel.None || Level < level) return;
            GetShortLevelAndColor(level, out var shortLevel, out var color);

            // Write bracketed short level with color, then message
            lock (Console.Out)
            {
                Console.Write('[');
                var previous = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(shortLevel);
                Console.ForegroundColor = previous;
                Console.WriteLine($"] {message}");
            }
        }

        // Writes only the colored bracketed short-level prefix (no newline).
        public void WriteLevelPrefix(LogLevel level)
        {
            GetShortLevelAndColor(level, out var shortLevel, out var color);
            var prev = Console.ForegroundColor;
            Console.Write('[');
            Console.ForegroundColor = color;
            Console.Write(shortLevel);
            Console.ForegroundColor = prev;
            Console.Write(']');
            Console.Write(' ');
        }

        public void Error(string message) => Write(LogLevel.Error, message);
        public void Warn(string message) => Write(LogLevel.Warn, message);
        public void Info(string message) => Write(LogLevel.Info, message);
        public void Debug(string message) => Write(LogLevel.Debug, message);
        public void Trace(string message) => Write(LogLevel.Trace, message);
    }
}
