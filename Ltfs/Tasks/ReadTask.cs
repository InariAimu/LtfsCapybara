using Ltfs.Index;

namespace Ltfs.Tasks;

public sealed class ReadTask : TaskBase
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public LtfsFile? SourceFile { get; set; }
}
