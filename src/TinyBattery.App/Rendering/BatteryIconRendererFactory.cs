using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.App.Rendering;

public sealed class BatteryIconRendererFactory : IBatteryIconRendererFactory
{
    private readonly IPluginAssetCatalog _plugins;

    public BatteryIconRendererFactory(IPluginAssetCatalog plugins)
    {
        _plugins = plugins;
    }

    public IBatteryIconRenderer Create(IconThemeDescriptor theme) => theme.Kind switch
    {
        IconThemeKind.Classic => new ClassicBatteryIconRenderer(
            theme.Classic ?? new ClassicIconThemeOptions(),
            theme.Id),
        IconThemeKind.ImageSet => new ImageSetBatteryIconRenderer(theme),
        IconThemeKind.Plugin => new PluginBatteryIconRenderer(
            theme,
            FindPlugin(theme.Id)),
        _ => throw new NotSupportedException(
            $"Icon theme kind '{theme.Kind}' is not supported by this build."),
    };

    private IBatteryIconThemePlugin FindPlugin(string themeId)
    {
        foreach (var plugin in _plugins.ThemePlugins)
        {
            if (string.Equals(
                    plugin.Describe().Id,
                    themeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return plugin;
            }
        }

        throw new InvalidOperationException(
            $"No theme plugin is loaded for '{themeId}'.");
    }
}
