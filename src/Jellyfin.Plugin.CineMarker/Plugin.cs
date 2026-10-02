using System;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Plugin entry point. Jellyfin finds it by scanning the assembly for <see cref="BasePlugin"/>.
/// </summary>
public sealed class Plugin : BasePlugin
{
    /// <inheritdoc />
    public override string Name => "CineMarker";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("81dd951c-751d-4736-b6a7-aa848a4cd741");

    /// <inheritdoc />
    public override string Description => "Replaces movie chapters with emoji markers from CineMarkerDB.";
}
