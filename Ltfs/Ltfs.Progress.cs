using System.Threading;

using Ltfs.Utils;

using TapeDrive;

namespace Ltfs;

public sealed class LtfsTapePerformanceSnapshot
{
    public int RepositionsPer100MB { get; init; }
    public double DataRateIntoBufferMBPerSecond { get; init; }
    public double MaximumDataRateMBPerSecond { get; init; }
    public double CurrentDataRateMBPerSecond { get; init; }
    public double NativeDataRateMBPerSecond { get; init; }
    public double CompressionRatio { get; init; }
}

public sealed class LtfsTaskProgressSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public required string QueueType { get; init; }
    public required int TotalItems { get; init; }
    public required int CompletedItems { get; init; }
    public required ulong TotalBytes { get; init; }
    public required ulong ProcessedBytes { get; init; }
    public string? CurrentItemPath { get; init; }
    public long CurrentItemBytes { get; init; }
    public long CurrentItemTotalBytes { get; init; }
    public double InstantBytesPerSecond { get; init; }
    public double AverageBytesPerSecond { get; init; }
    public double EstimatedRemainingSeconds { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public LtfsTapePerformanceSnapshot? TapePerformance { get; init; }
}

public static class LtfsProgressNumberSanitizer
{
    public static double Sanitize(double value, double fallback = -1d)
    {
        return double.IsFinite(value) ? value : fallback;
    }
}

public partial class Ltfs
{
    public event EventHandler<LtfsTaskProgressSnapshot>? ProgressUpdated;

    private LtfsTapePerformanceSnapshot? _lastTapePerformanceSnapshot;

    private void PublishProgress(LtfsTaskProgressSnapshot snapshot)
    {
        ProgressUpdated?.Invoke(this, snapshot);
        RenderProgress(snapshot);
    }

    private void RenderProgress(LtfsTaskProgressSnapshot snapshot)
    {
        if (Log.Current is ConsoleLogger cl)
        {
            try
            {
                lock (Console.Out)
                {
                    int bottom = Math.Max(0, Console.WindowHeight - 1);
                    try { Console.SetCursorPosition(0, bottom); } catch { }
                    try
                    {
                        Console.SetCursorPosition(0, bottom);
                        cl.WriteLevelPrefix(LogLevel.Info);
                        var toWrite = snapshot.StatusMessage.PadRight(Math.Max(0, Console.WindowWidth - 4));
                        Console.Write(toWrite);
                    }
                    catch { }
                }
            }
            catch { }

            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.StatusMessage))
            Logger.Info(snapshot.StatusMessage);
    }

    private async Task RunProgressMonitor(
        LtfsTaskQueueType queueType,
        int totalItems,
        Func<int> getCompletedItems,
        Func<ulong> getProcessedBytes,
        ulong totalBytes,
        Func<string?> getCurrentItemPath,
        Func<long> getCurrentItemBytes,
        Func<long> getCurrentItemTotalBytes,
        CancellationToken token)
    {
        var startTicks = DateTime.UtcNow.Ticks;
        long lastTicks = startTicks;
        ulong lastProcessed = getProcessedBytes();

        try
        {
            while (!token.IsCancellationRequested)
            {
                var tapePerformance = TryReadTapePerformanceSnapshot();
                PublishProgress(BuildProgressSnapshot(
                    queueType,
                    totalItems,
                    getCompletedItems(),
                    getProcessedBytes(),
                    totalBytes,
                    getCurrentItemPath(),
                    getCurrentItemBytes(),
                    getCurrentItemTotalBytes(),
                    tapePerformance,
                    startTicks,
                    ref lastTicks,
                    ref lastProcessed,
                    isCompleted: false));

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private LtfsTapePerformanceSnapshot? TryReadTapePerformanceSnapshot()
    {
        if (!_tapeDrive.TryReadPerformanceData(out var performance) || performance is null)
            return _lastTapePerformanceSnapshot;

        try
        {
            var snapshot = new LtfsTapePerformanceSnapshot
            {
                RepositionsPer100MB = performance.RepositionsPer100MB,
                DataRateIntoBufferMBPerSecond = LtfsProgressNumberSanitizer.Sanitize(performance.DataRateIntoBuffer),
                MaximumDataRateMBPerSecond = LtfsProgressNumberSanitizer.Sanitize(performance.MaximumDataRate),
                CurrentDataRateMBPerSecond = LtfsProgressNumberSanitizer.Sanitize(performance.CurrentDataRate),
                NativeDataRateMBPerSecond = LtfsProgressNumberSanitizer.Sanitize(performance.NativeDataRate),
                CompressionRatio = LtfsProgressNumberSanitizer.Sanitize(performance.CompressionRatio),
            };

            _lastTapePerformanceSnapshot = snapshot;
            return snapshot;
        }
        catch
        {
            return _lastTapePerformanceSnapshot;
        }
    }

    private LtfsTaskProgressSnapshot BuildProgressSnapshot(
        LtfsTaskQueueType queueType,
        int totalItems,
        int completedItems,
        ulong processedBytes,
        ulong totalBytes,
        string? currentItemPath,
        long currentItemBytes,
        long currentItemTotalBytes,
        LtfsTapePerformanceSnapshot? tapePerformance,
        long startTicks,
        ref long lastTicks,
        ref ulong lastProcessed,
        bool isCompleted)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var elapsedTicks = Math.Max(1, nowTicks - startTicks);
        var intervalTicks = Math.Max(1, nowTicks - lastTicks);
        var deltaBytes = processedBytes >= lastProcessed ? processedBytes - lastProcessed : 0ul;
        var averageBytesPerSecond = LtfsProgressNumberSanitizer.Sanitize(processedBytes * 10000000.0 / elapsedTicks, 0d);
        var instantBytesPerSecond = LtfsProgressNumberSanitizer.Sanitize(deltaBytes * 10000000.0 / intervalTicks, 0d);
        var remainingBytes = totalBytes > processedBytes ? totalBytes - processedBytes : 0ul;
        var etaSeconds = averageBytesPerSecond > 0
            ? LtfsProgressNumberSanitizer.Sanitize(remainingBytes / averageBytesPerSecond)
            : -1d;
        var percent = totalBytes > 0 ? processedBytes * 100.0 / totalBytes : 0.0;

        lastTicks = nowTicks;
        lastProcessed = processedBytes;

        return new LtfsTaskProgressSnapshot
        {
            QueueType = queueType.ToString(),
            TotalItems = totalItems,
            CompletedItems = completedItems,
            TotalBytes = totalBytes,
            ProcessedBytes = processedBytes,
            CurrentItemPath = currentItemPath,
            CurrentItemBytes = currentItemBytes,
            CurrentItemTotalBytes = currentItemTotalBytes,
            InstantBytesPerSecond = instantBytesPerSecond,
            AverageBytesPerSecond = averageBytesPerSecond,
            EstimatedRemainingSeconds = etaSeconds,
            IsCompleted = isCompleted,
            TapePerformance = tapePerformance,
            StatusMessage = $"{queueType}: {completedItems}/{totalItems} items, {FileSize.FormatSize(processedBytes)} / {FileSize.FormatSize(totalBytes)} {percent:f1}% ETA: {(etaSeconds < 0 ? "-" : etaSeconds.ToString("f0"))}s Speed: {FileSize.FormatSize((ulong)Math.Max(0, instantBytesPerSecond))}/s",
        };
    }
}