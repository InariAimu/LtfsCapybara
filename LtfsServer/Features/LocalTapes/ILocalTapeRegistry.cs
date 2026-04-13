using System.Collections.Generic;
using System.Threading.Tasks;

namespace LtfsServer.Features.LocalTapes;

public interface ILocalTapeRegistry
{
    /// <summary>
    /// Initialize the registry by scanning the given local path (AppData.Path/local).
    /// </summary>
    Task InitializeAsync(string localPath);

    /// <summary>
    /// Get all discovered tape names (directory names under local path).
    /// </summary>
    IEnumerable<string> GetTapeNames();

    /// <summary>
    /// Get CM-derived tape summaries keyed by tape name.
    /// </summary>
    IEnumerable<LocalTapeSummary> GetTapeSummaries();

    /// <summary>
    /// Get parsed file infos for a tape name.
    /// </summary>
    IEnumerable<TapeFileInfo> GetFiles(string tapeName);

    /// <summary>
    /// Parse and upsert a single file into the registry cache.
    /// </summary>
    bool TryUpsertFile(string tapeName, string filePath);
    
    /// <summary>
    /// Remove a single cached file record from the registry.
    /// </summary>
    bool TryRemoveFile(string tapeName, string filePath);
    
    /// <summary>
    /// Remove all cached records for a tape.
    /// </summary>
    bool TryRemoveTape(string tapeName);
}
