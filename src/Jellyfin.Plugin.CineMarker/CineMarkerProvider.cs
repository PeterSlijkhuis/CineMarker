using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Reads CineMarkerDB from GitHub without hammering it:
/// 1. One GitHub API call per run lists every movie file with its git blob SHA.
///    Movies without a file are never requested at all.
/// 2. Files are cached on disk. A cached file whose SHA still matches is reused,
///    so an unchanged database costs zero downloads.
/// 3. Downloads that are needed run one at a time with a fixed delay between them.
/// </summary>
public sealed class CineMarkerProvider
{
    private const string Repo = "PeterSlijkhuis/CineMarkerDB";
    private const string Branch = "main";
    private static readonly TimeSpan DownloadDelay = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CineMarkerProvider> _logger;
    private readonly string _cacheDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="CineMarkerProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Jellyfin's shared HTTP client factory (proxy and User-Agent already set).</param>
    /// <param name="applicationPaths">Server paths; the cache lives under the data folder.</param>
    /// <param name="logger">Logger.</param>
    public CineMarkerProvider(IHttpClientFactory httpClientFactory, IApplicationPaths applicationPaths, ILogger<CineMarkerProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cacheDir = Path.Combine(applicationPaths.DataPath, "cinemarker", "cache");
    }

    /// <summary>Lists the database: TMDB id to git blob SHA. One API request.</summary>
    public async Task<Dictionary<string, string>> GetIndexAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/git/trees/{Branch}?recursive=1");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client().SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var index = new Dictionary<string, string>();
        foreach (var entry in doc.RootElement.GetProperty("tree").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            if (path.StartsWith("movies/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal))
            {
                index[Path.GetFileNameWithoutExtension(path)] = entry.GetProperty("sha").GetString()!;
            }
        }

        return index;
    }

    /// <summary>Gets one movie file, from cache when its SHA matches. Returns null if it cannot be parsed.</summary>
    public async Task<MovieMarkers?> GetMarkersAsync(string tmdbId, string sha, CancellationToken cancellationToken)
    {
        var cachePath = Path.Combine(_cacheDir, tmdbId + ".json");
        byte[] bytes;
        if (File.Exists(cachePath) && GitBlobSha(bytes = await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false)) == sha)
        {
            _logger.LogDebug("Using cached markers for TMDB {TmdbId}", tmdbId);
        }
        else
        {
            await Task.Delay(DownloadDelay, cancellationToken).ConfigureAwait(false);
            bytes = await Client().GetByteArrayAsync($"https://raw.githubusercontent.com/{Repo}/{Branch}/movies/{tmdbId}.json", cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(_cacheDir);
            await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return Parse(bytes);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid CineMarker file for TMDB {TmdbId}", tmdbId);
            return null;
        }
    }

    /// <summary>Parses a movie file. Throws <see cref="JsonException"/> on bad JSON.</summary>
    public static MovieMarkers? Parse(byte[] json) => JsonSerializer.Deserialize<MovieMarkers>(json, JsonOptions);

    /// <summary>The SHA git (and the GitHub tree API) gives a file: sha1("blob {length}\0" + content).</summary>
    public static string GitBlobSha(byte[] content)
    {
        var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
        return Convert.ToHexStringLower(SHA1.HashData([.. header, .. content]));
    }

    private HttpClient Client() => _httpClientFactory.CreateClient(NamedClient.Default);
}
