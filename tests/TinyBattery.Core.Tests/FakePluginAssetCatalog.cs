using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Core.Tests;

internal sealed class FakePluginAssetCatalog : IPluginAssetCatalog
{
    public List<string> DeviceDefinitions { get; } = [];

    public List<string> ThemeManifests { get; } = [];

    public List<IBatteryProtocol> RegisteredProtocols { get; } = [];

    public List<IBatteryIconThemePlugin> IconThemes { get; } = [];

    public List<PluginDiagnostic> PluginDiagnostics { get; } = [];

    public IReadOnlyList<string> DeviceDefinitionPaths => DeviceDefinitions;

    public IReadOnlyList<string> ThemeManifestPaths => ThemeManifests;

    public IReadOnlyList<IBatteryProtocol> Protocols => RegisteredProtocols;

    public IReadOnlyList<IBatteryIconThemePlugin> ThemePlugins => IconThemes;

    public IReadOnlyList<PluginDiagnostic> Diagnostics => PluginDiagnostics;
}
