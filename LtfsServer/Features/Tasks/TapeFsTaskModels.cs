using System.Text.Json.Serialization;
using Ltfs;
using Ltfs.Tasks;

namespace LtfsServer.Features.Tasks;

public sealed class TapeFsPathTask
{
    public bool IsDirectory { get; set; }
    public string Operation { get; set; } = TapeFsTaskType.Add;
    public string Path { get; set; } = "/";
    public string? NewPath { get; set; }
    public string LocalPath { get; set; } = string.Empty;
}

public sealed class TapeFsTaskGroup
{
    public string TapeBarcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<TapeFsTask> Tasks { get; set; } = [];
    public long UpdatedAtTicks { get; set; }
}

public sealed class TapeFsTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Type { get; set; } = string.Empty;
    public string TapeBarcode { get; set; } = string.Empty;
    public TapeFsPathTask? PathTask { get; set; }
    public ReadTask? ReadTask { get; set; }
    public VerifyTask? VerifyTask { get; set; }
    public FormatTask? FormatTask { get; set; }
    public long CreatedAtTicks { get; set; } = DateTime.UtcNow.Ticks;

    // Legacy fields are kept for backward-compatible loading of persisted JSON.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WriteTask? WriteTask { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TapeFsDirectoryTask? FolderTask { get; set; }
}

public sealed class TapeFsDirectoryTask
{
    public string TaskType { get; set; } = TapeFsTaskType.Add;
    public string Path { get; set; } = "/";
}
