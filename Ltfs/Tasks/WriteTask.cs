using Ltfs.Index;

namespace Ltfs.Tasks;

public sealed class WriteTask : PathTaskBase
{
    public required string LocalPath { get; set; }
    public required LtfsFile LtfsTargetPath { get; set; }
}
