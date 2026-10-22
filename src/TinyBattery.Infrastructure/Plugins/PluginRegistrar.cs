using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Infrastructure.Plugins;

internal sealed class PluginContext : IPluginContext
{
    public PluginContext(string pluginDirectory, ILoggerFactory loggerFactory)
    {
        PluginDirectory = pluginDirectory;
        LoggerFactory = loggerFactory;
    }

    public string PluginDirectory { get; }

    public ILoggerFactory LoggerFactory { get; }
}

internal sealed class PluginRegistrar : IPluginRegistrar
{
    private readonly PluginAssetCatalog _catalog;
    private readonly IProtocolRegistry _registry;
    private readonly string _pluginDirectory;
    private readonly ILogger _logger;

    public PluginRegistrar(
        PluginAssetCatalog catalog,
        IProtocolRegistry registry,
        string pluginDirectory,
        ILogger logger)
    {
        _catalog = catalog;
        _registry = registry;
        _pluginDirectory = pluginDirectory;
        _logger = logger;
    }

    public void AddProtocol(IBatteryProtocol protocol)
    {
        _catalog.AddProtocol(protocol);
        _registry.Register(protocol);
        _logger.LogInformation("Plugin registered protocol '{Kind}'.", protocol.Kind);
    }

    public void AddIconTheme(IBatteryIconThemePlugin theme)
    {
        _catalog.AddThemePlugin(theme);
        _logger.LogInformation("Plugin registered icon theme '{ThemeId}'.", theme.Describe().Id);
    }

    public void AddDeviceDefinitionPath(string path)
    {
        var files = PluginPathResolver.ExpandDeviceDefinitions(
            _pluginDirectory,
            path,
            out var error);
        foreach (var file in files)
        {
            _catalog.AddDeviceDefinitionPath(file);
        }

        if (error is not null)
        {
            _catalog.AddDiagnostic(_pluginDirectory, error);
            _logger.LogWarning("{Error}", error);
        }
    }

    public void AddThemeManifestPath(string path)
    {
        var files = PluginPathResolver.ExpandThemeManifests(
            _pluginDirectory,
            path,
            out var error);
        foreach (var file in files)
        {
            _catalog.AddThemeManifestPath(file);
        }

        if (error is not null)
        {
            _catalog.AddDiagnostic(_pluginDirectory, error);
            _logger.LogWarning("{Error}", error);
        }
    }
}
