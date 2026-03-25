using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LtfsServer.Services;

public sealed class LocalTapeRegistry : ILocalTapeRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentBag<TapeFileInfo>> _tapes = new();
    private readonly ILogger<LocalTapeRegistry> _logger;

    private static readonly Regex FilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_T(\\d+)\\.xml$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Alternate pattern: {Barcode}_P{Partition}_G{Generation}_L{Location}_{yyyyMMdd}_{HHmmss}.{fraction}.xml
    private static readonly Regex AltFilenameRegex = new Regex(
        "^(.+?)_P(\\d+)_G(\\d+)_L(\\d+)_(\\d{8})_(\\d{6}\\.\\d{1,7})\\.xml$",
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

            var dirs = Directory.EnumerateDirectories(localPath);
            foreach (var dir in dirs)
            {
                var tapeName = Path.GetFileName(dir) ?? string.Empty;
                _tapes.TryAdd(tapeName, new ConcurrentBag<TapeFileInfo>());

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*.xml");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate files in {dir}", dir);
                    continue;
                }

                foreach (var f in files)
                {
                    var name = Path.GetFileName(f) ?? string.Empty;
                    var m = FilenameRegex.Match(name);
                    int partition = -1, generation = -1;
                    long location = -1, ticks = -1;
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
                            _logger.LogDebug("Filename did not match known patterns: {file}", f);
                            continue;
                        }

                        barcode = m2.Groups[1].Value;
                        if (!int.TryParse(m2.Groups[2].Value, out partition)) partition = -1;
                        if (!int.TryParse(m2.Groups[3].Value, out generation)) generation = -1;
                        if (!long.TryParse(m2.Groups[4].Value, out location)) location = -1;

                        // Parse timestamp like 20260305_015542.1560956 -> DateTime -> Ticks
                        var datePart = m2.Groups[5].Value; // yyyyMMdd
                        var timePart = m2.Groups[6].Value; // HHmmss.fraction
                        var combined = $"{datePart}_{timePart}";
                        if (DateTime.TryParseExact(combined, "yyyyMMdd_HHmmss.FFFFFFF", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            ticks = dt.Ticks;
                        }
                        else
                        {
                            ticks = -1;
                        }
                    }

                    if (!string.Equals(barcode, tapeName, StringComparison.Ordinal))
                    {
                        _logger.LogDebug("Filename barcode {barcode} does not match directory {tapeName} for file {file}", barcode, tapeName, f);
                        continue;
                    }

                    var info = new TapeFileInfo
                    {
                        FileName = name,
                        Partition = partition,
                        Generation = generation,
                        LocationStartBlock = location,
                        Ticks = ticks
                    };

                    _tapes[tapeName].Add(info);
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
}

public class TapeFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public int Partition { get; set; }
    public int Generation { get; set; }
    public long LocationStartBlock { get; set; }
    public long Ticks { get; set; }
}
