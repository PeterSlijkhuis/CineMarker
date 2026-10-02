using System;
using System.Text;
using Xunit;

namespace Jellyfin.Plugin.CineMarker.Tests;

public class ChapterBuilderTests
{
    private static readonly long TwoHours = TimeSpan.FromHours(2).Ticks;

    private static MovieMarkers Movie(string runtime = "02:00:00", int version = 1) => new(
        version,
        118340,
        runtime,
        [
            new Marker("00:00:00", "intro", "Opening", null),
            new Marker("01:05:12.500", "scare", "The Hallway", "💀"),
        ]);

    [Fact]
    public void ConvertsTimesToTicksAndComposesNames()
    {
        var chapters = ChapterBuilder.Build(Movie(), TwoHours)!;

        Assert.Equal(0, chapters[0].StartPositionTicks);
        Assert.Equal("🎬 Opening", chapters[0].Name);
        Assert.Equal(39_125_000_000, chapters[1].StartPositionTicks); // 3912.5 s in 100 ns units
        Assert.Equal("💀 The Hallway", chapters[1].Name);
    }

    [Theory]
    [InlineData("02:01:59")] // within 2 minutes
    [InlineData("01:58:00")]
    public void AppliesWhenRuntimeIsClose(string runtime) => Assert.NotNull(ChapterBuilder.Build(Movie(runtime), TwoHours));

    [Fact]
    public void SkipsDifferentCut() => Assert.Null(ChapterBuilder.Build(Movie("02:15:00"), TwoHours));

    [Fact]
    public void SkipsUnknownRuntime() => Assert.Null(ChapterBuilder.Build(Movie(), null));

    [Fact]
    public void SkipsUnknownSchemaVersion() => Assert.Null(ChapterBuilder.Build(Movie(version: 2), TwoHours));

    [Fact]
    public void DetectsUnchangedChapters()
    {
        Assert.True(ChapterBuilder.SameChapters(ChapterBuilder.Build(Movie(), TwoHours)!, ChapterBuilder.Build(Movie(), TwoHours)!));
    }

    [Fact]
    public void ParsesDatabaseFile()
    {
        var json = """
            { "$schema": "../schema.json", "schema_version": 1, "tmdb_id": 578, "imdb_id": "tt0073195",
              "title": "Jaws", "year": 1975, "runtime": "02:04:00",
              "markers": [ { "start": "00:03:10", "type": "scare", "title": "Chrissie's Swim" } ] }
            """;
        var movie = CineMarkerProvider.Parse(Encoding.UTF8.GetBytes(json))!;

        Assert.Equal(1, movie.SchemaVersion);
        Assert.Equal("02:04:00", movie.Runtime);
        Assert.Equal("Chrissie's Swim", movie.Markers[0].Title);
        Assert.Null(movie.Markers[0].Emoji);
    }

    [Fact]
    public void GitBlobShaMatchesGit()
    {
        // `printf 'hello\n' | git hash-object --stdin`
        Assert.Equal("ce013625030ba8dba906f756967f9e9ca394464a", CineMarkerProvider.GitBlobSha(Encoding.ASCII.GetBytes("hello\n")));
    }
}
