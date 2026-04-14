using Microsoft.Extensions.Logging;

using LtfsLogLevel = Ltfs.LogLevel;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LtfsServer.BootStrap;

public sealed class LtfsServerLoggerBridge(ILogger logger) : Ltfs.ILtfsLogger
{
    private readonly ILogger _logger = logger;

    public LtfsLogLevel Level { get; set; } = LtfsLogLevel.Trace;

    public void Error(string message) => Write(LtfsLogLevel.Error, message);

    public void Warn(string message) => Write(LtfsLogLevel.Warn, message);

    public void Info(string message) => Write(LtfsLogLevel.Info, message);

    public void Debug(string message) => Write(LtfsLogLevel.Debug, message);

    public void Trace(string message) => Write(LtfsLogLevel.Trace, message);

    private void Write(LtfsLogLevel level, string message)
    {
        if (Level == LtfsLogLevel.None || Level < level)
            return;

        var mappedLevel = MapLogLevel(level);
        if (!_logger.IsEnabled(mappedLevel))
            return;

        _logger.Log(mappedLevel, "{Message}", message);
    }

    private static MsLogLevel MapLogLevel(LtfsLogLevel level)
    {
        return level switch
        {
            LtfsLogLevel.Error => MsLogLevel.Error,
            LtfsLogLevel.Warn => MsLogLevel.Warning,
            LtfsLogLevel.Info => MsLogLevel.Information,
            LtfsLogLevel.Debug => MsLogLevel.Debug,
            LtfsLogLevel.Trace => MsLogLevel.Trace,
            _ => MsLogLevel.None,
        };
    }
}