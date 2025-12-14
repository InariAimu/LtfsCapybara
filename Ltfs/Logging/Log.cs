using System;

namespace Ltfs
{
    // Static façade for logging. Consumers should set `Log.Current` to an
    // `ILtfsLogger` implementation (for example `Test.ConsoleLogger`).
    // If `Log.Current` is null, logging is a no-op.
    public static class Log
    {
        private static ILtfsLogger? current;

        public static ILtfsLogger? Current
        {
            get => current;
            set => current = value;
        }

        public static void SetLogger(ILtfsLogger logger) => Current = logger;

        private static bool IsEnabled(LogLevel level)
        {
            return Current != null && Current.Level != LogLevel.None && Current.Level >= level;
        }

        public static void Error(string message)
        {
            if (!IsEnabled(LogLevel.Error)) return;
            Current!.Error(message);
        }

        public static void Warn(string message)
        {
            if (!IsEnabled(LogLevel.Warn)) return;
            Current!.Warn(message);
        }

        public static void Info(string message)
        {
            if (!IsEnabled(LogLevel.Info)) return;
            Current!.Info(message);
        }

        public static void Debug(string message)
        {
            if (!IsEnabled(LogLevel.Debug)) return;
            Current!.Debug(message);
        }

        public static void Trace(string message)
        {
            if (!IsEnabled(LogLevel.Trace)) return;
            Current!.Trace(message);
        }
    }
}
