using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TinyBattery.App.Hosting;
using TinyBattery.App.Views;

namespace TinyBattery.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (IconExport.TryRun(e.Args))
        {
            Shutdown();
            return;
        }

        var hostArgs = e.Args
            .Where(arg => !string.Equals(
                arg,
                "--background",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var builder = Host.CreateApplicationBuilder(hostArgs);
        builder.Services.AddTinyBatteryApp();
        _host = builder.Build();
        await _host.StartAsync();

        if (e.Args.Any(arg => string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            var settings = _host.Services.GetRequiredService<SettingsWindow>();
            settings.Show();
            settings.Activate();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }
}
