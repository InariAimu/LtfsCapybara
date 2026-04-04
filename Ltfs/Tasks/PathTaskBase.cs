namespace Ltfs.Tasks;

public abstract class PathTaskBase : TaskBase
{
    public required string TargetPath { get; set; }
}