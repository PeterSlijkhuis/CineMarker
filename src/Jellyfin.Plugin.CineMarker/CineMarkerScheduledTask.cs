using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Dashboard &gt; Scheduled Tasks &gt; CineMarker &gt; "Apply CineMarker chapters".
/// For every movie with a CineMarkerDB file whose runtime matches, backs up the
/// current chapters and replaces them. Movies without a file keep their chapters.
/// Runs daily because a full metadata refresh re-reads chapters from the video file.
/// </summary>
public sealed class CineMarkerScheduledTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IChapterManager _chapterManager;
    private readonly CineMarkerProvider _provider;
    private readonly ChapterBackupStore _backups;
    private readonly ILogger<CineMarkerScheduledTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CineMarkerScheduledTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Server service: finds movies.</param>
    /// <param name="chapterManager">Server service: reads and writes chapters.</param>
    /// <param name="provider">Registered in <see cref="PluginServiceRegistrator"/>.</param>
    /// <param name="backups">Registered in <see cref="PluginServiceRegistrator"/>.</param>
    /// <param name="logger">Logger.</param>
    public CineMarkerScheduledTask(
        ILibraryManager libraryManager,
        IChapterManager chapterManager,
        CineMarkerProvider provider,
        ChapterBackupStore backups,
        ILogger<CineMarkerScheduledTask> logger)
    {
        _libraryManager = libraryManager;
        _chapterManager = chapterManager;
        _provider = provider;
        _backups = backups;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Apply CineMarker chapters";

    /// <inheritdoc />
    public string Key => "CineMarkerApply";

    /// <inheritdoc />
    public string Description => "Replaces movie chapters with markers from CineMarkerDB. Original chapters are backed up first.";

    /// <inheritdoc />
    public string Category => "CineMarker";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var index = await _provider.GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            Recursive = true,
        });

        int applied = 0;
        for (int i = 0; i < movies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(100.0 * i / movies.Count);

            var movie = movies[i];
            var tmdbId = movie.GetProviderId(MetadataProvider.Tmdb);
            if (tmdbId is null || !index.TryGetValue(tmdbId, out var sha))
            {
                continue;
            }

            try
            {
                if (await ApplyAsync(movie, tmdbId, sha, cancellationToken).ConfigureAwait(false))
                {
                    applied++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad file or failed download must not stop the other movies.
                _logger.LogWarning(ex, "CineMarker failed for {Name} (TMDB {TmdbId})", movie.Name, tmdbId);
            }
        }

        _logger.LogInformation("CineMarker updated chapters for {Count} movies", applied);
        progress.Report(100);
    }

    private async Task<bool> ApplyAsync(BaseItem movie, string tmdbId, string sha, CancellationToken cancellationToken)
    {
        var markers = await _provider.GetMarkersAsync(tmdbId, sha, cancellationToken).ConfigureAwait(false);
        var chapters = markers is null ? null : ChapterBuilder.Build(markers, movie.RunTimeTicks);
        if (chapters is null)
        {
            _logger.LogInformation("Skipping {Name}: CineMarker file is invalid or made for a different cut", movie.Name);
            return false;
        }

        var current = _chapterManager.GetChapters(movie.Id);
        if (ChapterBuilder.SameChapters(current, chapters))
        {
            return false;
        }

        await _backups.BackupIfMissingAsync(movie.Id, current, cancellationToken).ConfigureAwait(false);
        _chapterManager.SaveChapters(movie, chapters);
        return true;
    }
}
