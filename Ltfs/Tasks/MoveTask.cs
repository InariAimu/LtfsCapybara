namespace Ltfs.Tasks;

public sealed class MoveTask : TaskBase
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
}
