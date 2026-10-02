# CineMarker

Jellyfin plugin (server 12.1) that replaces movie chapters with emoji markers from
[CineMarkerDB](https://github.com/PeterSlijkhuis/CineMarkerDB).

## What it does

Two tasks appear under **Dashboard > Scheduled Tasks > CineMarker**:

- **Apply CineMarker chapters** (daily at 04:00). For each movie with a TMDB id and a
  matching `movies/{tmdb_id}.json` in CineMarkerDB, it backs up the current chapters
  and replaces them. Movies without a file keep their chapters. A file is skipped when
  its `runtime` differs from the local file by more than 2 minutes (different cut).
- **Restore original chapters** (manual). Puts every changed movie back to its backup.

Backups and the download cache live in `{jellyfin data}/cinemarker/`.

## Rate limits

One GitHub API call per run lists every file in CineMarkerDB with its git SHA, so only
movies that have a file are downloaded. Downloads are cached on disk and reused while the
SHA is unchanged, and the ones that are needed run one at a time, 250 ms apart.

## Build and test

```sh
dotnet build src/Jellyfin.Plugin.CineMarker -c Release
dotnet test tests/Jellyfin.Plugin.CineMarker.Tests
```

Manual install: copy `Jellyfin.Plugin.CineMarker.dll` from `bin/Release/net10.0/` to
`{jellyfin config}/plugins/CineMarker/` and restart Jellyfin.
