using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.State;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;
using TinyBattery.App.Views;

namespace TinyBattery.App.Tray;

public sealed class NotifyIconTrayPresenter : ITrayPresenter
{
    private readonly IBatteryIconService _icons;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IBatteryStateStore _state;
    private readonly IUserSettingsStore _settings;
    private readonly IServiceProvider _services;
    private readonly ILogger<NotifyIconTrayPresenter> _logger;

    private TaskbarIcon? _tray;
    private MenuItem? _batteryItem;
    private MenuItem? _devicesMenu;
    private SettingsWindow? _settingsWindow;
    private BatteryIconState _lastState = new(null);
    private string _lastToolTip = "TinyBattery · 未选择设备";
    private string? _lastDisplaySignature;
    private bool _useDefaultIcon = true;

    public NotifyIconTrayPresenter(
        IBatteryIconService icons,
        IDeviceDefinitionStore definitions,
        IBatteryStateStore state,
        IUserSettingsStore settings,
        IServiceProvider services,
        ILogger<NotifyIconTrayPresenter> logger)
    {
        _icons = icons;
        _definitions = definitions;
        _state = state;
        _settings = settings;
        _services = services;
        _logger = logger;
    }

    public void Start()
    {
        if (_tray is not null)
        {
            return;
        }

        _batteryItem = new MenuItem
        {
            Header = _lastToolTip,
            IsEnabled = false,
        };
        _devicesMenu = new MenuItem { Header = "设备" };

        var reload = new MenuItem { Header = "重新加载设备配置" };
        reload.Click += async (_, _) => await ReloadDefinitionsAsync();

        var settings = new MenuItem { Header = "设置" };
        settings.Click += (_, _) => ShowSettings();

        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        var menu = new ContextMenu();
        menu.Items.Add(_batteryItem);
        menu.Items.Add(_devicesMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(reload);
        menu.Items.Add(settings);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.Opened += (_, _) => RefreshFromState();

        _tray = new TaskbarIcon
        {
            Icon = _icons.GetDefaultIcon(),
            ToolTipText = _lastToolTip,
            ContextMenu = menu,
            Visibility = Visibility.Visible,
        };

        try
        {
            // The icon is created in code, not in a visual tree, so Loaded never fires.
            _tray.ForceCreate(enablesEfficiencyMode: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create the tray icon.");
        }

        _icons.ThemeChanged += OnThemeChanged;
        _state.Updated += OnStateUpdated;
        RefreshFromState();
        _logger.LogInformation(
            "Tray icon started with theme '{ThemeId}' (created={Created}).",
            _icons.ThemeId,
            _tray.IsCreated);
    }

    public void SetBattery(BatteryIconState state, string toolTip)
    {
        _lastState = state;
        _lastToolTip = toolTip;

        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(ApplyBattery);
            return;
        }

        ApplyBattery();
    }

    private void ApplyBattery()
    {
        if (_tray is null)
        {
            return;
        }

        _tray.Icon = _useDefaultIcon
            ? _icons.GetDefaultIcon()
            : _icons.GetIcon(_lastState);
        _tray.ToolTipText = _lastToolTip;
        if (_batteryItem is not null)
        {
            _batteryItem.Header = _lastToolTip;
        }
    }

    private void OnStateUpdated(object? sender, DeviceSnapshot snapshot)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            RefreshFromState();
        }
        else
        {
            dispatcher.BeginInvoke(RefreshFromState);
        }
    }

    private void RefreshFromState()
    {
        var snapshots = _state.Snapshot();
        var hasTheme = !string.Equals(
            _icons.ThemeId,
            IconThemeDescriptor.FallbackId,
            StringComparison.OrdinalIgnoreCase);
        var primary = TrayDisplaySelector.Select(
            snapshots,
            _settings.Current.PrimaryDeviceId);
        var useDefaultIcon = TrayDisplaySelector.ShouldUseDefaultIcon(
            _icons.ThemeId,
            primary);
        var signature = $"{_icons.ThemeId}|{primary?.LogicalDeviceId ?? "-"}|{useDefaultIcon}";

        if (!string.Equals(signature, _lastDisplaySignature, StringComparison.Ordinal))
        {
            _lastDisplaySignature = signature;
            if (useDefaultIcon)
            {
                _logger.LogInformation(
                    "Showing the default tray icon: {Reason}.",
                    hasTheme
                        ? "no device with a readable battery is selected"
                        : "no usable theme is installed or selected");
            }
            else
            {
                _logger.LogInformation(
                    "Showing theme '{ThemeId}' for '{Device}'.",
                    _icons.ThemeId,
                    primary!.DisplayName);
            }
        }

        _useDefaultIcon = useDefaultIcon;

        if (primary is null)
        {
            SetBattery(
                new BatteryIconState(null),
                hasTheme
                    ? "TinyBattery · 未选择设备"
                    : "TinyBattery · 未安装主题");
        }
        else
        {
            var battery = primary.Battery!;
            var tooltip = BuildToolTip(primary);
            if (!hasTheme)
            {
                tooltip += " · 未安装主题";
            }

            SetBattery(
                new BatteryIconState(
                    battery.Percent,
                    battery.Charging is ChargingState.Charging),
                tooltip);
        }

        if (_devicesMenu is not null)
        {
            _devicesMenu.Items.Clear();
            if (snapshots.Count == 0)
            {
                _devicesMenu.Items.Add(new MenuItem
                {
                    Header = "未发现设备",
                    IsEnabled = false,
                });
            }
            else
            {
                foreach (var snapshot in snapshots)
                {
                    _devicesMenu.Items.Add(new MenuItem
                    {
                        Header = BuildDeviceRow(snapshot),
                        IsEnabled = false,
                    });
                }
            }
        }
    }

    private static string BuildToolTip(DeviceSnapshot snapshot)
    {
        var text = snapshot.DisplayName;
        if (snapshot.Battery is { } battery)
        {
            if (battery.Percent is int percent)
            {
                text += $" · {percent}%";
            }
            else if (battery.VoltageMv is { } voltage)
            {
                text += $" · {voltage:0} mV";
            }

            text += battery.Charging switch
            {
                ChargingState.Charging => " · 充电中",
                ChargingState.Full => " · 已充满",
                ChargingState.Error => " · 电池异常",
                _ => string.Empty,
            };
        }

        text += snapshot.State switch
        {
            DeviceConnectionState.Asleep => " · 休眠",
            DeviceConnectionState.AccessDenied => " · 被占用",
            DeviceConnectionState.Unreachable => " · 未连接",
            DeviceConnectionState.Faulted => " · 读取失败",
            _ => string.Empty,
        };

        if (snapshot.IsStale)
        {
            text += " · 未更新";
        }

        return text;
    }

    private static string BuildDeviceRow(DeviceSnapshot snapshot)
    {
        var text = snapshot.DisplayName;
        if (snapshot.Battery?.Percent is int percent)
        {
            text += $"  {percent}%";
        }
        else if (snapshot.Battery?.VoltageMv is { } voltage)
        {
            text += $"  {voltage:0} mV";
        }
        else
        {
            text += "  --";
        }

        if (snapshot.Battery?.Charging is ChargingState.Charging)
        {
            text += "  充电中";
        }

        return text;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            RefreshFromState();
        }
        else
        {
            dispatcher.BeginInvoke(RefreshFromState);
        }
    }

    private async Task ReloadDefinitionsAsync()
    {
        await _definitions.ReloadAsync(CancellationToken.None);
        if (_batteryItem is not null)
        {
            _batteryItem.Header = $"已加载 {_definitions.Definitions.Count} 个设备定义";
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = _services.GetRequiredService<SettingsWindow>();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void Dispose()
    {
        _icons.ThemeChanged -= OnThemeChanged;
        _state.Updated -= OnStateUpdated;
        _settingsWindow?.Close();
        _tray?.Dispose();
        _tray = null;
    }
}
