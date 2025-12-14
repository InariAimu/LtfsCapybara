using System;

namespace Ltfs
{
    public enum LogLevel
    {
        None = 0,
        Error = 1,
        Warn = 2,
        Info = 3,
        Debug = 4,
        Trace = 5
    }

    public interface ILtfsLogger
    {
        LogLevel Level { get; set; }
        void Error(string message);
        void Warn(string message);
        void Info(string message);
        void Debug(string message);
        void Trace(string message);
    }
}
