using System;

namespace Ltfs
{
    // Backwards-compatible adapter: existing code that calls `Logger.*`
    // will be forwarded to the DI façade `Log`.
    public static class Logger
    {
        public static LogLevel Level
        {
            get => Log.Current?.Level ?? LogLevel.None;
            set
            {
                if (Log.Current != null)
                {
                    Log.Current.Level = value;
                }
            }
        }

        public static void Error(string message) => Log.Error(message);
        public static void Warn(string message) => Log.Warn(message);
        public static void Info(string message) => Log.Info(message);
        public static void Debug(string message) => Log.Debug(message);
        public static void Trace(string message) => Log.Trace(message);
    }
}
