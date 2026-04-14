using Ltfs.Index;

namespace Ltfs.Tasks;

public sealed class VerifyTask : TaskBase
{
    public string SourcePath { get; set; } = string.Empty;
    public bool IsDirectoryMarker { get; set; }
    public LtfsFile? SourceFile { get; set; }
    public bool VerificationSkipped { get; set; }
    public bool? VerificationPassed { get; set; }
    public string? ExpectedCrc64 { get; set; }
    public string? ActualCrc64 { get; set; }
    public string? VerificationMessage { get; set; }
}