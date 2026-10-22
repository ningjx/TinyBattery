using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Abstractions.Plugins;

public interface IPluginAssetCatalog
{
    IReadOnlyList<string> DeviceDefinitionPaths { get; }

    IReadOnlyList<string> ThemeManifestPaths { get; }

    IReadOnlyList<IBatteryProtocol> Protocols { get; }

    IReadOnlyList<IBatteryIconThemePlugin> ThemePlugins { get; }

    IReadOnlyList<PluginDiagnostic> Diagnostics { get; }
}

public sealed record PluginDiagnostic(
    string Source,
    string Message);
