using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyBattery.Abstractions.Assets;
using TinyBattery.Abstractions.Packages;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Transports;
using TinyBattery.Abstractions.Updating;
using TinyBattery.Infrastructure.Assets;
using TinyBattery.Infrastructure.Hid;
using TinyBattery.Infrastructure.Packages;
using TinyBattery.Infrastructure.Paths;
using TinyBattery.Infrastructure.Plugins;
using TinyBattery.Infrastructure.Settings;
using TinyBattery.Infrastructure.Updating;

namespace TinyBattery.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTinyBatteryInfrastructure(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IAppPaths, AppPaths>();
        services.TryAddSingleton<IUserSettingsStore, JsonUserSettingsStore>();
        services.TryAddSingleton<IInstalledPackageStore, JsonInstalledPackageStore>();
        services.TryAddSingleton<IImageAssetValidator, SystemDrawingImageAssetValidator>();
        services.TryAddSingleton<IUpdateService, UpdateService>();
        services.TryAddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        });

        services.TryAddSingleton<PluginManifestValidator>();
        services.TryAddSingleton<PluginAssetCatalog>();
        services.TryAddSingleton<IPluginAssetCatalog>(
            provider => provider.GetRequiredService<PluginAssetCatalog>());
        services.TryAddSingleton<IPluginLoader, PluginLoader>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDeviceTransport, HidSharpTransport>());

        return services;
    }
}
