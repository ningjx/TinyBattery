using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TinyBattery.Abstractions.Monitoring;
using TinyBattery.App.Monitoring;
using TinyBattery.App.Plugins;
using TinyBattery.App.Rendering;
using TinyBattery.App.Tray;
using TinyBattery.App.Updating;
using TinyBattery.App.Views;
using TinyBattery.Core;
using TinyBattery.Core.Monitoring;
using TinyBattery.Infrastructure;
using TinyBattery.Protocols;

namespace TinyBattery.App.Hosting;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddTinyBatteryApp(
        this IServiceCollection services)
    {
        services.AddTinyBatteryCore();
        services.AddTinyBatteryProtocols();
        services.AddTinyBatteryInfrastructure();

        services.TryAddSingleton<IBatteryIconRendererFactory, BatteryIconRendererFactory>();
        services.TryAddSingleton<IBatteryIconService, BatteryIconService>();
        services.TryAddSingleton<ITrayIconSizeProvider, TrayIconSizeProvider>();
        services.TryAddSingleton<IDefaultTrayIconProvider, DefaultTrayIconProvider>();
        services.TryAddSingleton<ITrayPresenter, NotifyIconTrayPresenter>();
        services.TryAddSingleton<UpdateState>();
        services.TryAddSingleton<BundledPluginInstaller>();

        services.AddHostedService<AppStartupService>();
        services.TryAddSingleton<DeviceMonitorService>();
        services.TryAddSingleton<IDeviceRefreshCoordinator>(
            provider => provider.GetRequiredService<DeviceMonitorService>());
        services.AddSingleton<IHostedService>(
            provider => provider.GetRequiredService<DeviceMonitorService>());
        services.AddHostedService<InputActivityMonitor>();
        services.AddHostedService<TrayHostedService>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<AboutWindow>();

        return services;
    }
}
