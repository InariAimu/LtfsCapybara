using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using LtoTape;

using Microsoft.Extensions.Logging;

namespace LtfsServer.Features.LocalTapes;

public sealed class LocalTapeRegistry : ILocalTapeRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TapeFileInfo>> _tapes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, LocalTapeSummary> _tapeSummaries = new(StringComparer.Ordinal);
    private readonly ILogger<LocalTapeRegistry> _logger;
    private const string TimestampFormat = "yyyyMMdd_HHmmss.FFFFFFF";

    private static readonly Regex FilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_T(\\d+)\\.xml$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AltFilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.xml$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CmFilenameRegex = new Regex(
        "^(.+?)_G(\\d+)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.cm$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CmBinaryFilenameRegex = new Regex(
        "^(.+?)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.cmbin$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public LocalTapeRegistry(ILogger<LocalTapeRegistry> logger)
    {
        _logger = logger;
    }

    public async Task InitializeAsync(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            _logger.LogWarning("Local path is empty; skipping local tape scan.");
            return;
        }

        try
        {
            Directory.CreateDirectory(localPath);
            _tapes.Clear();
            _tapeSummaries.Clear();

            var dirs = Directory.EnumerateDirectories(localPath);
            foreach (var dir in dirs)
            {
                var tapeName = Path.GetFileName(dir) ?? string.Empty;
                _tapes[tapeName] = new ConcurrentDictionary<string, TapeFileInfo>(StringComparer.OrdinalIgnoreCase);

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*.*")
                        .Where(static file =>
                            file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".cm", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate files in {dir}", dir);
                    continue;
                }

                foreach (var filePath in files)
                {
                    TryUpsertFile(tapeName, filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning local tapes at {path}", localPath);
            throw;
        }

        await Task.CompletedTask;
    }

    public bool TryUpsertFile(string tapeName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(tapeName) || string.IsNullOrWhiteSpace(filePath))
            return false;

        var files = GetOrCreateFiles(tapeName);

        if (TryParseXmlFileInfo(tapeName, filePath, out TapeFileIndex? xmlInfo))
        {
            files[filePath] = new TapeFileInfo { Index = xmlInfo };
            return true;
        }

        if (TryParseCmFileInfo(tapeName, filePath, out TapeFileIndex? cmInfo))
        {
            files[filePath] = new TapeFileInfo { Index = cmInfo };

            try
            {
                var cartridgeMemory = new CartridgeMemory();
                if (filePath.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
                    cartridgeMemory.FromBinaryFile(filePath);
                else
                    cartridgeMemory.FromLcgCmFile(filePath);

                var summary = BuildSummary(tapeName, cmInfo, cartridgeMemory);
                UpsertSummary(summary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse cartridge memory file {file}", filePath);
            }

            return true;
        }

        _logger.LogDebug("Filename did not match known patterns: {file}", filePath);
        return false;
    }

    public bool TryRemoveFile(string tapeName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(tapeName) || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!_tapes.TryGetValue(tapeName, out var files))
        {
            return false;
        }

        var removed = files.TryRemove(filePath, out _);
        if (!removed)
        {
            return false;
        }

        if (files.IsEmpty)
        {
            _tapes.TryRemove(tapeName, out _);
            _tapeSummaries.TryRemove(tapeName, out _);
            return true;
        }

        RefreshSummaryFromCache(tapeName, files);
        return true;
    }

    public bool TryRemoveTape(string tapeName)
    {
        if (string.IsNullOrWhiteSpace(tapeName))
        {
            return false;
        }

        var removedFiles = _tapes.TryRemove(tapeName, out _);
        var removedSummary = _tapeSummaries.TryRemove(tapeName, out _);
        return removedFiles || removedSummary;
    }

    public IEnumerable<string> GetTapeNames()
    {
        return _tapes.Keys.OrderBy(k => k);
    }

    public IEnumerable<LocalTapeSummary> GetTapeSummaries()
    {
        return _tapeSummaries.Values
            .OrderBy(v => v.TapeName, StringComparer.Ordinal)
            .ToArray();
    }

    public IEnumerable<TapeFileInfo> GetFiles(string tapeName)
    {
        if (string.IsNullOrEmpty(tapeName))
            return Array.Empty<TapeFileInfo>();

        if (_tapes.TryGetValue(tapeName, out var files))
            return files.Values.ToArray();

        return Array.Empty<TapeFileInfo>();
    }

    private ConcurrentDictionary<string, TapeFileInfo> GetOrCreateFiles(string tapeName)
    {
        return _tapes.GetOrAdd(
            tapeName,
            _ => new ConcurrentDictionary<string, TapeFileInfo>(StringComparer.OrdinalIgnoreCase));
    }

    private bool TryParseXmlFileInfo(string tapeName, string filePath, out TapeFileIndex info)
    {
        var name = Path.GetFileName(filePath) ?? string.Empty;
        var m = FilenameRegex.Match(name);
        int partition = -1;
        int generation = -1;
        long location = -1;
        long ticks = -1;
        string barcode;

        if (m.Success)
        {
            barcode = m.Groups[1].Value;
            if (!int.TryParse(m.Groups[2].Value, out partition)) partition = -1;
            if (!int.TryParse(m.Groups[3].Value, out generation)) generation = -1;
            if (!long.TryParse(m.Groups[4].Value, out location)) location = -1;
            if (!long.TryParse(m.Groups[5].Value, out ticks)) ticks = -1;
        }
        else
        {
            var m2 = AltFilenameRegex.Match(name);
            if (!m2.Success)
            {
                info = new TapeFileIndex();
                return false;
            }

            barcode = m2.Groups[1].Value;
            if (!int.TryParse(m2.Groups[2].Value, out partition)) partition = -1;
            if (!int.TryParse(m2.Groups[3].Value, out generation)) generation = -1;
            if (!long.TryParse(m2.Groups[4].Value, out location)) location = -1;
            ticks = ParseTimestampTicks(m2.Groups[5].Value, m2.Groups[6].Value);
        }

        if (!string.Equals(barcode, tapeName, StringComparison.Ordinal))
        {
            _logger.LogDebug("Filename barcode {barcode} does not match directory {tapeName} for file {file}", barcode, tapeName, filePath);
            info = new TapeFileIndex();
            return false;
        }

        info = new TapeFileIndex
        {
            FileName = name,
            Partition = partition,
            Generation = generation,
            LocationStartBlock = location,
            Ticks = ticks
        };

        return true;
    }

    private bool TryParseCmFileInfo(string tapeName, string filePath, out TapeFileIndex info)
    {
        var name = Path.GetFileName(filePath) ?? string.Empty;
        var cmMatch = CmFilenameRegex.Match(name);
        if (cmMatch.Success)
        {
            var barcode = cmMatch.Groups[1].Value;
            if (!string.Equals(barcode, tapeName, StringComparison.Ordinal))
            {
                _logger.LogDebug("Filename barcode {barcode} does not match directory {tapeName} for file {file}", barcode, tapeName, filePath);
                info = new TapeFileIndex();
                return false;
            }

            int generation = -1;
            if (!int.TryParse(cmMatch.Groups[2].Value, out generation))
                generation = -1;

            info = new TapeFileIndex
            {
                FileName = name,
                Partition = -1,
                Generation = generation,
                LocationStartBlock = -1,
                Ticks = ParseTimestampTicks(cmMatch.Groups[3].Value, cmMatch.Groups[4].Value)
            };

            return true;
        }

        var cmBinMatch = CmBinaryFilenameRegex.Match(name);
        if (!cmBinMatch.Success)
        {
            info = new TapeFileIndex();
            return false;
        }

        var cmBinBarcode = cmBinMatch.Groups[1].Value;
        if (!string.Equals(cmBinBarcode, tapeName, StringComparison.Ordinal))
        {
            _logger.LogDebug("Filename barcode {barcode} does not match directory {tapeName} for file {file}", cmBinBarcode, tapeName, filePath);
            info = new TapeFileIndex();
            return false;
        }

        info = new TapeFileIndex
        {
            FileName = name,
            Partition = -1,
            Generation = -1,
            LocationStartBlock = -1,
            Ticks = ParseTimestampTicks(cmBinMatch.Groups[2].Value, cmBinMatch.Groups[3].Value)
        };

        return true;
    }

    private static long ParseTimestampTicks(string datePart, string timePart)
    {
        var combined = $"{datePart}_{timePart}";
        return DateTime.TryParseExact(combined, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.Ticks
            : -1;
    }

    private static LocalTapeSummary BuildSummary(string tapeName, TapeFileIndex index, CartridgeMemory cartridgeMemory)
    {
        var generation = cartridgeMemory.Manufacturer.Gen;
        var particleType = cartridgeMemory.Manufacturer.ParticleType.ToString();
        var vendor = (cartridgeMemory.Manufacturer.TapeVendor ?? string.Empty).Trim();

        CalculatePartitionSizes(cartridgeMemory, out var totalSizeBytes, out var freeSizeBytes);

        return new LocalTapeSummary
        {
            TapeName = tapeName,
            CmFileName = index.FileName,
            Generation = generation,
            ParticleType = particleType,
            Vendor = vendor,
            TotalSizeBytes = totalSizeBytes,
            FreeSizeBytes = freeSizeBytes,
            Ticks = index.Ticks
        };
    }

    private static void CalculatePartitionSizes(CartridgeMemory cartridgeMemory, out long totalSizeBytes, out long freeSizeBytes)
    {
        totalSizeBytes = 0;
        freeSizeBytes = 0;

        foreach (var partition in cartridgeMemory.Partitions.Values)
        {
            var allocated = Math.Max(0L, partition.AllocatedSize);
            var used = Math.Max(0L, partition.UsedSize);
            var estimatedLoss = Math.Max(0L, partition.EstimatedLossSize);

            totalSizeBytes += allocated;

            var available = allocated - used - estimatedLoss;
            freeSizeBytes += Math.Max(0L, available);
        }
    }

    private void UpsertSummary(LocalTapeSummary incoming)
    {
        _tapeSummaries.AddOrUpdate(
            incoming.TapeName,
            incoming,
            (_, existing) => incoming.Ticks >= existing.Ticks ? incoming : existing);
    }

    private void RefreshSummaryFromCache(
        string tapeName,
        ConcurrentDictionary<string, TapeFileInfo>? files = null)
    {
        files ??= _tapes.TryGetValue(tapeName, out var existingFiles)
            ? existingFiles
            : null;

        if (files is null || files.IsEmpty)
        {
            _tapeSummaries.TryRemove(tapeName, out _);
            return;
        }

        var latestCm = files
            .Where(static entry => entry.Value.Index.FileName.EndsWith(".cm", StringComparison.OrdinalIgnoreCase)
                || entry.Value.Index.FileName.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Value.Index.Ticks)
            .FirstOrDefault();

        if (latestCm.Equals(default(KeyValuePair<string, TapeFileInfo>)))
        {
            _tapeSummaries.TryRemove(tapeName, out _);
            return;
        }

        try
        {
            var cartridgeMemory = new CartridgeMemory();
            if (latestCm.Key.EndsWith(".cmbin", StringComparison.OrdinalIgnoreCase))
                cartridgeMemory.FromBinaryFile(latestCm.Key);
            else
                cartridgeMemory.FromLcgCmFile(latestCm.Key);

            var summary = BuildSummary(tapeName, latestCm.Value.Index, cartridgeMemory);
            UpsertSummary(summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh cartridge memory summary for {tapeName} from {file}", tapeName, latestCm.Key);
            _tapeSummaries.TryRemove(tapeName, out _);
        }
    }
}

public class TapeFileInfo
{
    public TapeFileIndex Index { get; set; } = new();
}

public class TapeFileIndex
{
    public string FileName { get; set; } = string.Empty;
    public int Partition { get; set; }
    public int Generation { get; set; }
    public long LocationStartBlock { get; set; }
    public long Ticks { get; set; }
}

public class LocalTapeSummary
{
    public string TapeName { get; set; } = string.Empty;
    public string CmFileName { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string ParticleType { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long FreeSizeBytes { get; set; }
    public long Ticks { get; set; }
}
