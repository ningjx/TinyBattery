using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyBattery.App.Rendering;
using TinyBattery.App.Tray;
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

        services.AddHostedService<AppStartupService>();
        services.AddHostedService<DeviceMonitorService>();
        services.AddHostedService<TrayHostedService>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<AboutWindow>();

        return services;
    }
}
