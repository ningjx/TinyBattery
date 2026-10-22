using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Infrastructure.Plugins;

public sealed class PluginAssetCatalog : IPluginAssetCatalog
{
    private readonly object _gate = new();
    private readonly HashSet<string> _devicePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _themeManifestPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IBatteryProtocol> _protocols = [];
    private readonly List<IBatteryIconThemePlugin> _themePlugins = [];
    private readonly List<PluginDiagnostic> _diagnostics = [];

    public IReadOnlyList<string> DeviceDefinitionPaths
    {
        get
        {
            lock (_gate)
            {
                return _devicePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
    }

    public IReadOnlyList<string> ThemeManifestPaths
    {
        get
        {
            lock (_gate)
            {
                return _themeManifestPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
    }

    public IReadOnlyList<IBatteryProtocol> Protocols
    {
        get
        {
            lock (_gate)
            {
                return _protocols.ToArray();
            }
        }
    }

    public IReadOnlyList<IBatteryIconThemePlugin> ThemePlugins
    {
        get
        {
            lock (_gate)
            {
                return _themePlugins.ToArray();
            }
        }
    }

    public IReadOnlyList<PluginDiagnostic> Diagnostics
    {
        get
        {
            lock (_gate)
            {
                return _diagnostics.ToArray();
            }
        }
    }

    public void AddDeviceDefinitionPath(string path)
    {
        lock (_gate)
        {
            _devicePaths.Add(path);
        }
    }

    public void AddThemeManifestPath(string path)
    {
        lock (_gate)
        {
            _themeManifestPaths.Add(path);
        }
    }

    public void AddProtocol(IBatteryProtocol protocol)
    {
        lock (_gate)
        {
            _protocols.Add(protocol);
        }
    }

    public void AddThemePlugin(IBatteryIconThemePlugin theme)
    {
        lock (_gate)
        {
            _themePlugins.Add(theme);
        }
    }

    public void AddDiagnostic(string source, string message)
    {
        lock (_gate)
        {
            _diagnostics.Add(new PluginDiagnostic(source, message));
        }
    }
}
