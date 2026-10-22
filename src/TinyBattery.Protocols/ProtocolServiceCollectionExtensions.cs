using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Protocols.Hid;

namespace TinyBattery.Protocols;

public static class ProtocolServiceCollectionExtensions
{
    public static IServiceCollection AddTinyBatteryProtocols(
        this IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IBatteryProtocol, RequestResponseProtocol>());
        services.TryAddSingleton<IProtocolRegistry, ProtocolRegistry>();
        return services;
    }
}
