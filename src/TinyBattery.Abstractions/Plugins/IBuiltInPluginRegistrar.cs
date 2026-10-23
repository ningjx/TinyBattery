using TinyBattery.Abstractions.Plugins;

namespace TinyBattery.Abstractions.Plugins;

public interface IBuiltInPluginRegistrar
{
    void Register(ITinyBatteryPlugin plugin, string pluginDirectory);
}
