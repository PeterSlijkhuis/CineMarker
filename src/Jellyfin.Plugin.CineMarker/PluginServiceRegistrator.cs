using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CineMarker;

/// <summary>
/// Adds this plugin's own services to Jellyfin's DI container.
/// Scheduled tasks need no registration: Jellyfin discovers every IScheduledTask in the
/// assembly and builds it with constructor injection, resolving ILibraryManager,
/// IChapterManager, IHttpClientFactory, IApplicationPaths and ILogger&lt;T&gt; from the server,
/// and the two singletons below from here.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<CineMarkerProvider>();
        serviceCollection.AddSingleton<ChapterBackupStore>();
    }
}
