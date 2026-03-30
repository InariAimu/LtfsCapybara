using System.Runtime.InteropServices;

namespace LtfsServer.Services;

public sealed class LocalFileSystemTreeService : ILocalFileSystemTreeService
{
    private const int DiscoveryTimeoutMs = 5000;
    private const int PerServerTimeoutMs = 1000;
    private const int NetworkCacheMinutes = 3;
    private const int MaxServers = 32;
    private const int MaxSharesPerServer = 32;

    private readonly ILogger<LocalFileSystemTreeService> _logger;
    private readonly object _networkCacheLock = new();
    private List<LocalFsNode> _networkCache = new();
    private DateTime _networkCacheAtUtc = DateTime.MinValue;

    public LocalFileSystemTreeService(ILogger<LocalFileSystemTreeService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<LocalFsNode>> GetRootsAsync(CancellationToken cancellationToken = default)
    {
        var roots = new List<LocalFsNode>();

        foreach (var drive in DriveInfo.GetDrives().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var path = NormalizeDriveRootPath(drive.Name);
            var id = MakeNodeId(path);
            var name = drive.Name.TrimEnd('\\');

            if (drive.DriveType == DriveType.Network)
            {
                roots.Add(new LocalFsNode(id, name, path, "network", true, true));
                continue;
            }

            var isReady = drive.IsReady;
            roots.Add(new LocalFsNode(
                id,
                name,
                path,
                "drive",
                isReady,
                isReady,
                isReady ? null : "Drive is not ready"
            ));
        }

        var networkRoots = await GetDiscoveredNetworkRootsAsync(cancellationToken);
        roots.AddRange(networkRoots);

        return roots
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Kind, StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<LocalFsChildrenResult> GetChildrenAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeAndValidatePath(path);

        IEnumerable<string> childDirectories;
        try
        {
            childDirectories = Directory.EnumerateDirectories(normalized);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new LocalFsChildrenResult(
                normalized,
                Array.Empty<LocalFsNode>(),
                "Access denied"
            ));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate directories at path {Path}", normalized);
            return Task.FromResult(new LocalFsChildrenResult(
                normalized,
                Array.Empty<LocalFsNode>(),
                ex.Message
            ));
        }

        var children = new List<LocalFsNode>();
        foreach (var child in childDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name))
            {
                name = child;
            }

            var hasChildren = false;
            var available = true;
            string? error = null;

            try
            {
                using var enumerator = Directory.EnumerateDirectories(child).GetEnumerator();
                hasChildren = enumerator.MoveNext();
            }
            catch (UnauthorizedAccessException)
            {
                available = false;
                error = "Access denied";
            }
            catch (IOException)
            {
                // A node can still be selected even if probing children failed.
                hasChildren = false;
            }

            children.Add(new LocalFsNode(
                MakeNodeId(child),
                name,
                child,
                "dir",
                available,
                hasChildren,
                error
            ));
        }

        var ordered = children.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        return Task.FromResult(new LocalFsChildrenResult(normalized, ordered));
    }

    public Task<LocalFsFilesResult> GetFilesAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeAndValidatePath(path);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(normalized);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new LocalFsFilesResult(normalized, Array.Empty<LocalFsFile>(), "Access denied"));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate files at path {Path}", normalized);
            return Task.FromResult(new LocalFsFilesResult(normalized, Array.Empty<LocalFsFile>(), ex.Message));
        }

        var ordered = files
            .Select(path =>
            {
                long size = 0;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch
                {
                    // Ignore file metadata failures and keep default size.
                }

                return new LocalFsFile(Path.GetFileName(path), path, size);
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(new LocalFsFilesResult(normalized, ordered));
    }

    private async Task<IReadOnlyList<LocalFsNode>> GetDiscoveredNetworkRootsAsync(CancellationToken cancellationToken)
    {
        var snapshot = GetNetworkCacheIfFresh();
        if (snapshot is not null)
        {
            return snapshot;
        }

        try
        {
            var discovered = await DiscoverNetworkRootsAsync(cancellationToken);
            SetNetworkCache(discovered);
            return discovered;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Network share discovery failed.");
            var stale = GetNetworkCacheRegardlessAge();
            return stale;
        }
    }

    private List<LocalFsNode>? GetNetworkCacheIfFresh()
    {
        lock (_networkCacheLock)
        {
            if (DateTime.UtcNow - _networkCacheAtUtc <= TimeSpan.FromMinutes(NetworkCacheMinutes))
            {
                return _networkCache.ToList();
            }

            return null;
        }
    }

    private List<LocalFsNode> GetNetworkCacheRegardlessAge()
    {
        lock (_networkCacheLock)
        {
            return _networkCache.ToList();
        }
    }

    private void SetNetworkCache(IReadOnlyList<LocalFsNode> items)
    {
        lock (_networkCacheLock)
        {
            _networkCache = items.ToList();
            _networkCacheAtUtc = DateTime.UtcNow;
        }
    }

    private async Task<IReadOnlyList<LocalFsNode>> DiscoverNetworkRootsAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DiscoveryTimeoutMs);

        var discoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodes = new List<LocalFsNode>();

        // Include mapped network drives immediately.
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Network))
        {
            var path = NormalizeDriveRootPath(drive.Name);
            if (!discoveredPaths.Add(path))
            {
                continue;
            }

            nodes.Add(new LocalFsNode(
                MakeNodeId(path),
                drive.Name.TrimEnd('\\'),
                path,
                "network",
                drive.IsReady,
                drive.IsReady,
                drive.IsReady ? null : "Drive is not ready"
            ));
        }

        if (!OperatingSystem.IsWindows())
        {
            return nodes;
        }

        // Enumerate servers with timeout - NetServerEnum can block indefinitely
        IReadOnlyList<string> servers;
        try
        {
            var serversTask = Task.Run(() => EnumerateLanServers(), timeoutCts.Token);
            var completed = await Task.WhenAny(serversTask, Task.Delay(DiscoveryTimeoutMs / 2, timeoutCts.Token));
            if (completed != serversTask)
            {
                _logger.LogWarning("NetServerEnum timed out after {TimeoutMs}ms - Browse service may not be available or network is slow", DiscoveryTimeoutMs / 2);
                return nodes;
            }
            servers = serversTask.Result;
            _logger.LogInformation("NetServerEnum discovered {ServerCount} servers", servers.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Network server enumeration was cancelled");
            return nodes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate network servers");
            return nodes;
        }

        if (servers.Count > 0)
        {
            _logger.LogDebug("Discovered servers: {Servers}", string.Join(", ", servers));
        }

        foreach (var server in servers.Take(MaxServers))
        {
            try
            {
                timeoutCts.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Network discovery was cancelled");
                break;
            }

            IReadOnlyList<string>? shares = null;
            try
            {
                _logger.LogDebug("Enumerating shares for server {Server}", server);
                var sharesTask = Task.Run(() => EnumerateDiskShares(server, MaxSharesPerServer), timeoutCts.Token);
                var completed = await Task.WhenAny(sharesTask, Task.Delay(PerServerTimeoutMs, timeoutCts.Token));
                if (completed != sharesTask)
                {
                    _logger.LogDebug("Timeout enumerating shares for server {Server}", server);
                    continue;
                }

                shares = sharesTask.Result;
                if (shares.Count > 0)
                {
                    _logger.LogDebug("Found {ShareCount} shares on {Server}: {Shares}", shares.Count, server, string.Join(", ", shares));
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Network discovery was cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to enumerate shares for server {Server}", server);
                continue;
            }

            foreach (var sharePath in shares ?? Array.Empty<string>())
            {
                if (!discoveredPaths.Add(sharePath))
                {
                    continue;
                }

                var label = sharePath.StartsWith("\\\\", StringComparison.Ordinal)
                    ? sharePath[2..]
                    : sharePath;
                nodes.Add(new LocalFsNode(
                    MakeNodeId(sharePath),
                    label,
                    sharePath,
                    "network",
                    true,
                    true
                ));
            }
        }

        return nodes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string NormalizeDriveRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':')
        {
            return path + @"\";
        }

        return path;
    }

    private static string NormalizeAndValidatePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ArgumentException("Path is required.", nameof(rawPath));
        }

        var decoded = Uri.UnescapeDataString(rawPath.Trim());
        if (decoded.Length == 2 && char.IsLetter(decoded[0]) && decoded[1] == ':')
        {
            decoded += @"\";
        }

        var candidate = decoded.StartsWith("\\\\", StringComparison.Ordinal)
            ? decoded
            : decoded.Replace('/', '\\');

        string normalized;
        try
        {
            normalized = Path.GetFullPath(candidate);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid path: {ex.Message}", nameof(rawPath), ex);
        }

        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException($"Path does not exist: {normalized}");
        }

        return normalized;
    }

    private static string MakeNodeId(string path)
    {
        return path;
    }

    private static IReadOnlyList<string> EnumerateLanServers()
    {
        var servers = new List<string>();
        IntPtr buffer = IntPtr.Zero;
        try
        {
            var entriesRead = 0;
            var totalEntries = 0;
            var resumeHandle = 0;

            var result = NetServerEnum(
                null,
                100,
                out buffer,
                MAX_PREFERRED_LENGTH,
                ref entriesRead,
                ref totalEntries,
                SV_TYPE_WORKSTATION | SV_TYPE_SERVER,
                null,
                ref resumeHandle);

            if (result != NERR_Success && result != ERROR_MORE_DATA)
            {
                // NetServerEnum failed - this is common if Browse service is not running
                // or if the computer is not part of a workgroup/domain
                return servers;
            }

            var structSize = Marshal.SizeOf<SERVER_INFO_100>();
            for (var i = 0; i < entriesRead; i++)
            {
                var current = IntPtr.Add(buffer, i * structSize);
                var serverInfo = Marshal.PtrToStructure<SERVER_INFO_100>(current);
                if (!string.IsNullOrWhiteSpace(serverInfo.sv100_name))
                {
                    servers.Add(serverInfo.sv100_name);
                }
            }

            return servers;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                NetApiBufferFree(buffer);
            }
        }
    }

    private static IReadOnlyList<string> EnumerateDiskShares(string serverName, int limit)
    {
        var shares = new List<string>();
        IntPtr buffer = IntPtr.Zero;

        try
        {
            var entriesRead = 0;
            var totalEntries = 0;
            var resumeHandle = 0;

            // NetShareEnum requires the server name in UNC format (\\servername)
            var uncServerName = serverName.StartsWith("\\\\", StringComparison.Ordinal)
                ? serverName
                : $"\\\\{serverName}";

            var result = NetShareEnum(
                uncServerName,
                1,
                out buffer,
                MAX_PREFERRED_LENGTH,
                ref entriesRead,
                ref totalEntries,
                ref resumeHandle);

            if (result != NERR_Success && result != ERROR_MORE_DATA)
            {
                // Common error codes:
                // 5 = ERROR_ACCESS_DENIED
                // 53 = ERROR_BAD_NETPATH (network path not found)
                // 124 = ERROR_INVALID_LEVEL
                return shares;
            }

            var structSize = Marshal.SizeOf<SHARE_INFO_1>();
            for (var i = 0; i < entriesRead && shares.Count < limit; i++)
            {
                var current = IntPtr.Add(buffer, i * structSize);
                var shareInfo = Marshal.PtrToStructure<SHARE_INFO_1>(current);
                if (shareInfo.shi1_type != STYPE_DISKTREE)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(shareInfo.shi1_netname) || shareInfo.shi1_netname.EndsWith("$", StringComparison.Ordinal))
                {
                    continue;
                }

                shares.Add($"{uncServerName}\\{shareInfo.shi1_netname}");
            }

            return shares;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                NetApiBufferFree(buffer);
            }
        }
    }

    private const int MAX_PREFERRED_LENGTH = -1;
    private const int NERR_Success = 0;
    private const int ERROR_MORE_DATA = 234;
    private const int SV_TYPE_WORKSTATION = 0x00000001;
    private const int SV_TYPE_SERVER = 0x00000002;
    private const uint STYPE_DISKTREE = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVER_INFO_100
    {
        public int sv100_platform_id;
        public string sv100_name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHARE_INFO_1
    {
        public string shi1_netname;
        public uint shi1_type;
        public string shi1_remark;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetServerEnum(
        string? servername,
        int level,
        out IntPtr bufptr,
        int prefmaxlen,
        ref int entriesread,
        ref int totalentries,
        int servertype,
        string? domain,
        ref int resume_handle);

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetShareEnum(
        string? servername,
        int level,
        out IntPtr bufptr,
        int prefmaxlen,
        ref int entriesread,
        ref int totalentries,
        ref int resume_handle);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
