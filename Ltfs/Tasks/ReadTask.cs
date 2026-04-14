using Ltfs.Index;

namespace Ltfs.Tasks;

public sealed class ReadTask : TaskBase
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public bool IsDirectoryMarker { get; set; }
    public bool IntegrityCheckFailed { get; set; }
    public string? FailureMessage { get; set; }
    public string? PreservedTargetPath { get; set; }
    public LtfsFile? SourceFile { get; set; }
}
