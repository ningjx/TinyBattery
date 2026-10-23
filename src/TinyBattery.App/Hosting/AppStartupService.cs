using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Startup;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Abstractions.Updating;
using TinyBattery.App.Plugins;
using TinyBattery.App.Updating;
using TinyBattery.Infrastructure.Paths;

namespace TinyBattery.App.Hosting;

public sealed class AppStartupService : IHostedService
{
    private readonly IAppPaths _paths;
    private readonly IPluginLoader _plugins;
    private readonly BundledPluginInstaller _bundledPlugins;
    private readonly IUserSettingsStore _settings;
    private readonly IAutoStartService _autoStart;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;
    private readonly IUpdateService _updates;
    private readonly UpdateState _updateState;
    private readonly ILogger<AppStartupService> _logger;

    public AppStartupService(
        IAppPaths paths,
        IPluginLoader plugins,
        BundledPluginInstaller bundledPlugins,
        IUserSettingsStore settings,
        IAutoStartService autoStart,
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes,
        IUpdateService updates,
        UpdateState updateState,
        ILogger<AppStartupService> logger)
    {
        _paths = paths;
        _plugins = plugins;
        _bundledPlugins = bundledPlugins;
        _settings = settings;
        _autoStart = autoStart;
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

        try
        {
            _bundledPlugins.Install();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not install bundled plugins.");
        }

        await _plugins.LoadAsync(cancellationToken);
        await _settings.LoadAsync(cancellationToken);
        TryEnsureAutoStart();
        await _definitions.ReloadAsync(cancellationToken);
        await _themes.ReloadAsync(cancellationToken);

        if (_settings.Current.CheckForUpdatesOnStartup
            && !string.IsNullOrWhiteSpace(_settings.Current.UpdateFeedUrl))
        {
            _ = CheckUpdatesInBackgroundAsync();
        }
    }

    private void TryEnsureAutoStart()
    {
        try
        {
            if (_settings.Current.StartWithWindows)
            {
                _autoStart.EnsureEnabled();
            }
            else if (_autoStart.IsEnabled)
            {
                _autoStart.SetEnabled(false);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not reconcile the auto-start entry.");
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
