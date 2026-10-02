using System.Collections.Generic;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>One movie file from CineMarkerDB (movies/{tmdb_id}.json, schema v1).</summary>
public sealed record MovieMarkers(int SchemaVersion, int TmdbId, string Runtime, IReadOnlyList<Marker> Markers);

/// <summary>One marker. <see cref="Emoji"/> overrides the default for <see cref="Type"/>.</summary>
public sealed record Marker(string Start, string Type, string Title, string? Emoji);
