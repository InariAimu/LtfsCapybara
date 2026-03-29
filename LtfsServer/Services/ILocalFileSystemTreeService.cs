namespace LtfsServer.Services;

public sealed record LocalFsNode(
    string Id,
    string Name,
    string Path,
    string Kind,
    bool Available,
    bool HasChildren,
    string? Error = null
);

public sealed record LocalFsChildrenResult(
    string ParentPath,
    IReadOnlyList<LocalFsNode> Children,
    string? Warning = null
);

public interface ILocalFileSystemTreeService
{
    Task<IReadOnlyList<LocalFsNode>> GetRootsAsync(CancellationToken cancellationToken = default);

    Task<LocalFsChildrenResult> GetChildrenAsync(string path, CancellationToken cancellationToken = default);
}
