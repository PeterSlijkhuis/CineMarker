using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Dashboard &gt; Scheduled Tasks &gt; CineMarker &gt; "Restore original chapters".
/// Puts every backed-up movie back to its original chapters. Manual only.
/// Run it before uninstalling, or the daily task will not be there to undo anything.
/// </summary>
public sealed class RestoreChaptersTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterManager _chapterManager;
    private readonly ChapterBackupStore _backups;
    private readonly ILogger<RestoreChaptersTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RestoreChaptersTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Server service: looks up movies by id.</param>
    /// <param name="chapterManager">Server service: writes chapters.</param>
    /// <param name="backups">Registered in <see cref="PluginServiceRegistrator"/>.</param>
    /// <param name="logger">Logger.</param>
    public RestoreChaptersTask(ILibraryManager libraryManager, IChapterManager chapterManager, ChapterBackupStore backups, ILogger<RestoreChaptersTask> logger)
    {
        _libraryManager = libraryManager;
        _chapterManager = chapterManager;
        _backups = backups;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Restore original chapters";

    /// <inheritdoc />
    public string Key => "CineMarkerRestore";

    /// <inheritdoc />
    public string Description => "Undoes CineMarker: restores the chapters each movie had before CineMarker changed them.";

    /// <inheritdoc />
    public string Category => "CineMarker";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        int restored = 0;
        foreach (var id in _backups.List())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var movie = _libraryManager.GetItemById(id);
            if (movie is null)
            {
                continue;
            }

            _chapterManager.SaveChapters(movie, await _backups.LoadAsync(id, cancellationToken).ConfigureAwait(false));
            _backups.Delete(id);
            restored++;
        }

        _logger.LogInformation("CineMarker restored original chapters for {Count} movies", restored);
        progress.Report(100);
    }
}
