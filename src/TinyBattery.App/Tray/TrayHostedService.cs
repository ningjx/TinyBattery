using System.Windows;
using Microsoft.Extensions.Hosting;

namespace TinyBattery.App.Tray;

public sealed class TrayHostedService : IHostedService
{
    private readonly ITrayPresenter _presenter;

    public TrayHostedService(ITrayPresenter presenter)
    {
        _presenter = presenter;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            _presenter.Start();
        }
        else
        {
            dispatcher.Invoke(_presenter.Start);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            _presenter.Dispose();
        }
        else
        {
            dispatcher.Invoke(_presenter.Dispose);
        }

        return Task.CompletedTask;
    }
}
