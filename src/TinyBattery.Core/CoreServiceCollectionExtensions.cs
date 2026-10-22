using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.State;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Importing;
using TinyBattery.Core.Matching;
using TinyBattery.Core.Registry;
using TinyBattery.Core.State;
using TinyBattery.Core.Themes;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddTinyBatteryCore(this IServiceCollection services)
    {
        services.TryAddSingleton<DefinitionValidator>();
        services.TryAddSingleton<IDeviceDefinitionStore, JsonDeviceDefinitionStore>();
        services.TryAddSingleton<IDeviceMatcher, DeviceMatcher>();
        services.TryAddSingleton<IBatteryStateStore, InMemoryBatteryStateStore>();

        services.TryAddSingleton<IconThemeValidator>();
        services.TryAddSingleton<IconThemeLoader>();
        services.TryAddSingleton<IIconThemeStore, JsonIconThemeStore>();

        services.TryAddSingleton<SafeZipExtractor>();
        services.TryAddSingleton<IDeviceDefinitionImporter, DeviceDefinitionImporter>();
        services.TryAddSingleton<IIconThemeImporter, IconThemeImporter>();
        services.TryAddSingleton<PackageValidator>();
        services.TryAddSingleton<IPackageImporter, PackageImporter>();

        return services;
    }
}
