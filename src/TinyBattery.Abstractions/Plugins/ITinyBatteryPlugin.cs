using Microsoft.Extensions.Logging;

namespace TinyBattery.Abstractions.Plugins;

public interface ITinyBatteryPlugin
{
    string Id { get; }

    Version ApiVersion { get; }

    void Register(IPluginContext context, IPluginRegistrar registrar);
}

public interface IPluginContext
{
    string PluginDirectory { get; }

    ILoggerFactory LoggerFactory { get; }
}

public interface IPluginRegistrar
{
    void AddProtocol(Protocols.IBatteryProtocol protocol);

    void AddIconTheme(Themes.IBatteryIconThemePlugin theme);

    void AddDeviceDefinitionPath(string path);

    void AddThemeManifestPath(string path);
}
