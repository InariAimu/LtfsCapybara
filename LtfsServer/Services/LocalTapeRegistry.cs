using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LtoTape;

namespace LtfsServer.Services;

public sealed class LocalTapeRegistry : ILocalTapeRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TapeFileInfo>> _tapes = new();
    private readonly ILogger<LocalTapeRegistry> _logger;
    private const string TimestampFormat = "yyyyMMdd_HHmmss.FFFFFFF";

    private static readonly Regex FilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_T(\\d+)\\.xml$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Alternate pattern: {Barcode}_P{Partition}_G{Generation}_L{Location}_{yyyyMMdd}_{HHmmss}.{fraction}.xml
    private static readonly Regex AltFilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.xml$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CmFilenameRegex = new Regex(
        "^(.+?)_G(\\d+)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.cm$",
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
            // Ensure directory exists; do not create if missing (but create to be permissive)
            Directory.CreateDirectory(localPath);
            _tapes.Clear();

            var dirs = Directory.EnumerateDirectories(localPath);
            foreach (var dir in dirs)
            {
                var tapeName = Path.GetFileName(dir) ?? string.Empty;
                var bag = new ConcurrentBag<TapeFileInfo>();
                _tapes[tapeName] = bag;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*.*")
                        .Where(static file =>
                            file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".cm", StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate files in {dir}", dir);
                    continue;
                }

                foreach (var f in files)
                {
                    if (TryParseXmlFileInfo(tapeName, f, out TapeFileIndex? xmlInfo))
                    {
                        bag.Add(new TapeFileInfo { Index = xmlInfo });
                        continue;
                    }

                    if (TryParseCmFileInfo(f, out TapeFileIndex? cmInfo))
                    {
                        var info = new TapeFileInfo { Index = cmInfo };

                        try
                        {
                            var cartridgeMemory = new CartridgeMemory();
                            cartridgeMemory.FromLcgCmFile(f);
                            info.CartridgeMemory = cartridgeMemory;
                            bag.Add(info);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse cartridge memory file {file}", f);
                        }

                        continue;
                    }

                    _logger.LogDebug("Filename did not match known patterns: {file}", f);
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

    public IEnumerable<string> GetTapeNames()
    {
        return _tapes.Keys.OrderBy(k => k);
    }

    public IEnumerable<TapeFileInfo> GetFiles(string tapeName)
    {
        if (string.IsNullOrEmpty(tapeName))
            return Array.Empty<TapeFileInfo>();

        if (_tapes.TryGetValue(tapeName, out var bag))
            return bag.ToArray();

        return Array.Empty<TapeFileInfo>();
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

    private bool TryParseCmFileInfo(string filePath, out TapeFileIndex info)
    {
        var name = Path.GetFileName(filePath) ?? string.Empty;
        var match = CmFilenameRegex.Match(name);
        if (!match.Success)
        {
            info = new TapeFileIndex();
            return false;
        }

        int generation = -1;
        if (!int.TryParse(match.Groups[2].Value, out generation))
            generation = -1;

        info = new TapeFileIndex
        {
            FileName = name,
            Partition = -1,
            Generation = generation,
            LocationStartBlock = -1,
            Ticks = ParseTimestampTicks(match.Groups[3].Value, match.Groups[4].Value)
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
}

public class TapeFileInfo
{
    public TapeFileIndex Index { get; set; } = new();
    public CartridgeMemory? CartridgeMemory { get; set; }
}

public class TapeFileIndex
{
    public string FileName { get; set; } = string.Empty;
    public int Partition { get; set; }
    public int Generation { get; set; }
    public long LocationStartBlock { get; set; }
    public long Ticks { get; set; }
}
