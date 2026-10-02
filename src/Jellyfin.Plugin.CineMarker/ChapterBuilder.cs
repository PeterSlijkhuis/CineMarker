using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>Turns a CineMarkerDB file into Jellyfin chapters.</summary>
public static class ChapterBuilder
{
    /// <summary>How far the local file's runtime may differ from the timed cut.</summary>
    public static readonly TimeSpan RuntimeTolerance = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<string, string> DefaultEmoji = new()
    {
        ["intro"] = "🎬",
        ["music"] = "🎵",
        ["scare"] = "👻",
        ["action"] = "💥",
        ["plot"] = "📖",
        ["credits"] = "🎞️",
        ["post_credits"] = "⭐",
        ["other"] = "📍",
    };

    /// <summary>
    /// Builds the chapter list, or returns null when the file must not be applied:
    /// unknown schema version, no markers, or a runtime that does not match the local cut.
    /// </summary>
    public static List<ChapterInfo>? Build(MovieMarkers movie, long? itemRunTimeTicks)
    {
        if (movie.SchemaVersion != 1 || movie.Markers is not { Count: > 0 } || itemRunTimeTicks is null)
        {
            return null;
        }

        if (Math.Abs(itemRunTimeTicks.Value - ToTicks(movie.Runtime)) > RuntimeTolerance.Ticks)
        {
            return null;
        }

        return movie.Markers
            .Select(m => new ChapterInfo
            {
                // TimeSpan.Ticks are 100 ns units, the same unit Jellyfin stores.
                StartPositionTicks = ToTicks(m.Start),
                Name = $"{m.Emoji ?? DefaultEmoji.GetValueOrDefault(m.Type, "📍")} {m.Title}",
            })
            .OrderBy(c => c.StartPositionTicks)
            .ToList();
    }

    /// <summary>True when both lists have the same starts and names, so saving would change nothing.</summary>
    public static bool SameChapters(IReadOnlyList<ChapterInfo> a, IReadOnlyList<ChapterInfo> b)
        => a.Count == b.Count
           && a.Zip(b).All(p => p.First.StartPositionTicks == p.Second.StartPositionTicks && p.First.Name == p.Second.Name);

    /// <summary>"hh:mm:ss" or "hh:mm:ss.mmm" to ticks.</summary>
    public static long ToTicks(string time) => TimeSpan.Parse(time, CultureInfo.InvariantCulture).Ticks;
}
