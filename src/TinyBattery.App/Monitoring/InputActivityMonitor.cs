using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TinyBattery.Abstractions.Monitoring;

namespace TinyBattery.App.Monitoring;

public sealed class InputActivityMonitor : IHostedService, IDisposable
{
    private const int IdleThresholdMs = 3000;
    private const int RefreshDebounceMs = 2000;

    private readonly IDeviceRefreshCoordinator _coordinator;
    private readonly ILogger<InputActivityMonitor> _logger;
    private readonly CancellationTokenSource _cts = new();

    private Task? _loop;
    private long _lastRefreshTick;
    private bool _wasIdle = true;
    private bool _subscribed;

    public InputActivityMonitor(
        IDeviceRefreshCoordinator coordinator,
        ILogger<InputActivityMonitor> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _subscribed = true;
        _loop = Task.Run(() => RunAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscribed)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _subscribed = false;
        }

        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (GetLastInputInfo(out var info))
                {
                    var idle = unchecked((uint)Environment.TickCount) - info.dwTime;
                    var isIdle = idle >= IdleThresholdMs;
                    if (_wasIdle && !isIdle)
                    {
                        TriggerRefresh("user input resumed");
                    }

                    _wasIdle = isIdle;
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Could not read the last input time.");
            }

            try
            {
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            TriggerRefresh("system resumed");
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock
            or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteConnect)
        {
            TriggerRefresh($"session {e.Reason}");
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        TriggerRefresh("display settings changed");

    private void TriggerRefresh(string reason)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastRefreshTick);
        if (now - last < RefreshDebounceMs)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastRefreshTick, now, last) != last)
        {
            return;
        }

        _logger.LogDebug("Refreshing device sessions: {Reason}.", reason);
        _coordinator.RequestRefresh();
    }

    private static bool GetLastInputInfo(out LastInputInfo info)
    {
        info = new LastInputInfo
        {
            cbSize = (uint)Marshal.SizeOf<LastInputInfo>(),
        };
        return NativeMethods.GetLastInputInfo(ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetLastInputInfo(ref LastInputInfo plii);
    }
}
