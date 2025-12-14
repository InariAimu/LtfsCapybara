using System;

namespace Ltfs.Utils;

/// <summary>
/// Utility helpers for formatting file sizes into human-readable strings.
/// Uses 1024-based units and outputs units: B, K, M, G, T.
/// </summary>
public static class FileSize
{
    /// <summary>
    /// Formats a byte count into a compact string using units B, K, M, G, T.
    /// Examples: 512 -> "512 B", 1536 -> "1.5 K", 1048576 -> "1 M".
    /// </summary>
    /// <param name="bytes">Number of bytes (non-negative).</param>
    /// <returns>Human-readable size string.</returns>
    public static string FormatSize(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        return FormatSize((decimal)bytes);
    }

    /// <summary>
    /// Formats an unsigned byte count into a compact string using units B, K, M, G, T.
    /// </summary>
    public static string FormatSize(ulong bytes) => FormatSize((decimal)bytes);

    /// <summary>
    /// Core implementation using decimal to avoid overflow and provide accurate rounding.
    /// </summary>
    public static string FormatSize(decimal bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        const decimal Unit = 1024m;
        if (bytes < Unit)
            return string.Format("{0:0} B", bytes);

        string[] units = { "KB", "MB", "GB", "TB" };
        decimal value = bytes;
        int idx = -1;
        while (value >= Unit && idx < units.Length - 1)
        {
            value /= Unit;
            idx++;
        }

        var unit = units[Math.Max(0, idx)];
        // Use up to two decimal places, but drop trailing zeros
        return string.Format("{0:0.##} {1}", value, unit);
    }
}
