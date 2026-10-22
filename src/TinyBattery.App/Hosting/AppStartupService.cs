using Microsoft.Extensions.Hosting;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Infrastructure.Paths;

namespace TinyBattery.App.Hosting;

public sealed class AppStartupService : IHostedService
{
    private readonly IAppPaths _paths;
    private readonly IPluginLoader _plugins;
    private readonly IUserSettingsStore _settings;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;

    public AppStartupService(
        IAppPaths paths,
        IPluginLoader plugins,
        IUserSettingsStore settings,
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes)
    {
        _paths = paths;
        _plugins = plugins;
        _settings = settings;
        _definitions = definitions;
        _themes = themes;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_paths is AppPaths appPaths)
        {
            appPaths.EnsureCreated();
        }

        await _plugins.LoadAsync(cancellationToken);
        await _settings.LoadAsync(cancellationToken);
        await _definitions.ReloadAsync(cancellationToken);
        await _themes.ReloadAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
