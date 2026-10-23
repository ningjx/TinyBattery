using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Plugin.Logitech;

namespace TinyBattery.App.Plugins;

public sealed class BundledPluginInstaller
{
    private const string LogitechResource =
        "TinyBattery.Bundled.logitech.devices.g913-tkl.json";
    private const string ClassicThemeResource =
        "TinyBattery.Bundled.classic.theme.json";
    private const string ClassicIconResource =
        "TinyBattery.Bundled.classic.icon.svg";

    private readonly IAppPaths _paths;
    private readonly IPluginAssetCatalog _catalog;
    private readonly IBuiltInPluginRegistrar _registrar;
    private readonly ILogger<BundledPluginInstaller> _logger;

    public BundledPluginInstaller(
        IAppPaths paths,
        IPluginAssetCatalog catalog,
        IBuiltInPluginRegistrar registrar,
        ILogger<BundledPluginInstaller> logger)
    {
        _paths = paths;
        _catalog = catalog;
        _registrar = registrar;
        _logger = logger;
    }

    public void Install()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";
        var root = Path.Combine(_paths.RootDirectory, "bundled", version);

        if (!ExternalPluginExists("logitech"))
        {
            var directory = Path.Combine(root, "logitech");
            var devicePath = Path.Combine(directory, "devices", "g913-tkl.json");
            WriteResource(LogitechResource, devicePath);
            _catalog.AddDeviceDefinitionPath(devicePath);
            _registrar.Register(new LogitechPlugin(), directory);
        }

        if (!ExternalPluginExists("classic-theme"))
        {
            var directory = Path.Combine(root, "classic-theme");
            var themePath = Path.Combine(directory, "theme.json");
            WriteResource(ClassicThemeResource, themePath);
            WriteResource(ClassicIconResource, Path.Combine(directory, "icon.svg"));
            _catalog.AddThemeManifestPath(themePath);
        }

        _logger.LogInformation("Bundled plugins are available from '{Directory}'.", root);
    }

    private bool ExternalPluginExists(string id)
    {
        foreach (var root in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "plugins"),
                     TinyBattery.Infrastructure.Plugins.PluginLoader.PortablePluginsDirectory(),
                     _paths.PluginsDirectory,
                 })
        {
            if (File.Exists(Path.Combine(root, id, "plugin.json")))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteResource(string resourceName, string path)
    {
        var assembly = typeof(BundledPluginInstaller).Assembly;
        using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Bundled plugin resource '{resourceName}' was not found.");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var file = File.Create(path);
        resource.CopyTo(file);
    }
}
