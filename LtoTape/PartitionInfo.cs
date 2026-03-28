namespace LtoTape;

public class PartitionInfo
{
    public int Id { get; set; } = 0;
    public int WrapCount { get; set; } = 0;
    public long AllocatedSize { get; set; } = 0;
    public long UsedSize { get; set; } = 0;
    public long EstimatedLossSize { get; set; } = 0;
}
