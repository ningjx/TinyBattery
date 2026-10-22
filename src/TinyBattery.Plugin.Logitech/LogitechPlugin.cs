using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Plugins;

namespace TinyBattery.Plugin.Logitech;

public sealed class LogitechPlugin : ITinyBatteryPlugin
{
    public string Id => "logitech";

    public Version ApiVersion => new(1, 0, 0);

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddProtocol(
            new Hidpp2BatteryProtocol(
                context.LoggerFactory.CreateLogger<Hidpp2BatteryProtocol>()));
    }
}
