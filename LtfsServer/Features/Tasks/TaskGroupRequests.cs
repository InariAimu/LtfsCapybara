using Ltfs;
using Ltfs.Tasks;

namespace LtfsServer.Features.Tasks;

public sealed class TapeFsTaskCreateRequest
{
    public string Type { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public TapeFsPathTask? PathTask { get; set; }
    public ReadTask? ReadTask { get; set; }
    public VerifyTask? VerifyTask { get; set; }
    public FormatTask? FormatTask { get; set; }
}

public sealed class AddTapeFsServerFolderTaskRequest
{
    public string LocalPath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
}

public sealed class RenameTapeFsTaskGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class AddTapeFsFormatTaskRequest
{
    public FormatTask? FormatTask { get; set; }
}
