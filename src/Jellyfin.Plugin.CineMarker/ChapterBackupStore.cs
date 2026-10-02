using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Keeps each movie's original chapters as {data}/cinemarker/backups/{itemId}.json.
/// A backup is written once and never overwritten, so it always holds the chapters
/// from before CineMarker first touched the movie.
/// </summary>
public sealed class ChapterBackupStore
{
    private readonly string _dir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChapterBackupStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Server paths.</param>
    public ChapterBackupStore(IApplicationPaths applicationPaths)
    {
        _dir = Path.Combine(applicationPaths.DataPath, "cinemarker", "backups");
    }

    /// <summary>Saves the chapters unless a backup for this item already exists.</summary>
    public async Task BackupIfMissingAsync(Guid itemId, IReadOnlyList<ChapterInfo> chapters, CancellationToken cancellationToken)
    {
        var path = PathFor(itemId);
        if (File.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(chapters), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ids of every movie that has a backup.</summary>
    public IEnumerable<Guid> List()
        => Directory.Exists(_dir)
            ? Directory.EnumerateFiles(_dir, "*.json").Select(f => Guid.Parse(Path.GetFileNameWithoutExtension(f)))
            : [];

    /// <summary>Reads a backup.</summary>
    public async Task<List<ChapterInfo>> LoadAsync(Guid itemId, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<List<ChapterInfo>>(await File.ReadAllTextAsync(PathFor(itemId), cancellationToken).ConfigureAwait(false)) ?? [];

    /// <summary>Removes a backup after it has been restored.</summary>
    public void Delete(Guid itemId) => File.Delete(PathFor(itemId));

    private string PathFor(Guid itemId) => Path.Combine(_dir, itemId.ToString("N") + ".json");
}
