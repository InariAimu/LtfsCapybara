using Ltfs;
using Ltfs.Tasks;

namespace LtfsServer.Features.Tasks;

internal static class FormatTaskDefaults
{
    public static FormatTask NormalizeFormatTask(FormatTask? formatTask, string fallbackBarcode, string fallbackVolumeName)
    {
        return new FormatTask
        {
            FormatParam = NormalizeFormatParam(formatTask?.FormatParam, fallbackBarcode, fallbackVolumeName),
        };
    }

    public static FormatParam NormalizeFormatParam(FormatParam? formatParam, string fallbackBarcode, string fallbackVolumeName)
    {
        var normalized = formatParam ?? new FormatParam();

        normalized.Barcode = string.IsNullOrWhiteSpace(normalized.Barcode)
            ? fallbackBarcode.Trim().ToUpperInvariant()
            : normalized.Barcode.Trim().ToUpperInvariant();

        normalized.VolumeName = string.IsNullOrWhiteSpace(normalized.VolumeName)
            ? (string.IsNullOrWhiteSpace(fallbackVolumeName) ? normalized.Barcode : fallbackVolumeName.Trim())
            : normalized.VolumeName.Trim();

        normalized.MediaPool ??= string.Empty;
        normalized.ExtraPartitionCount = normalized.ExtraPartitionCount == 0 ? (byte)1 : normalized.ExtraPartitionCount;
        normalized.BlockSize = normalized.BlockSize == 0 ? 524288UL : normalized.BlockSize;
        normalized.P0Size = normalized.P0Size == 0 ? (ushort)1 : normalized.P0Size;
        normalized.P1Size = normalized.P1Size == 0 ? (ushort)0xffff : normalized.P1Size;

        return normalized;
    }
}