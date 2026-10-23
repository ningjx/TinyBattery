using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Abstractions.Updating;
using TinyBattery.App.Updating;
using TinyBattery.Infrastructure.Paths;

namespace TinyBattery.App.Hosting;

public sealed class AppStartupService : IHostedService
{
    private readonly IAppPaths _paths;
    private readonly IPluginLoader _plugins;
    private readonly IUserSettingsStore _settings;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;
    private readonly IUpdateService _updates;
    private readonly UpdateState _updateState;
    private readonly ILogger<AppStartupService> _logger;

    public AppStartupService(
        IAppPaths paths,
        IPluginLoader plugins,
        IUserSettingsStore settings,
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes,
        IUpdateService updates,
        UpdateState updateState,
        ILogger<AppStartupService> logger)
    {
        _paths = paths;
        _plugins = plugins;
        _settings = settings;
        _definitions = definitions;
        _themes = themes;
        _updates = updates;
        _updateState = updateState;
        _logger = logger;
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

        if (_settings.Current.CheckForUpdatesOnStartup
            && !string.IsNullOrWhiteSpace(_settings.Current.UpdateFeedUrl))
        {
            _ = CheckUpdatesInBackgroundAsync();
        }
    }

    private async Task CheckUpdatesInBackgroundAsync()
    {
        try
        {
            var result = await _updates.CheckAsync(CancellationToken.None);
            _updateState.Set(result.Updates.Count);
            if (result.Updates.Count > 0)
            {
                _logger.LogInformation(
                    "Background update check found {Count} update(s).",
                    result.Updates.Count);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Background update check failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
