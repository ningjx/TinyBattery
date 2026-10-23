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
using TinyBattery.App.Updating;
using TinyBattery.App.Views;

namespace TinyBattery.App.Tray;

public sealed class NotifyIconTrayPresenter : ITrayPresenter
{
    private readonly IBatteryIconService _icons;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IBatteryStateStore _state;
    private readonly IUserSettingsStore _settings;
    private readonly IIconThemeStore _themes;
    private readonly UpdateState _updateState;
    private readonly IServiceProvider _services;
    private readonly ILogger<NotifyIconTrayPresenter> _logger;

    private TaskbarIcon? _tray;
    private MenuItem? _batteryItem;
    private MenuItem? _devicesMenu;
    private MenuItem? _updatesItem;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private UpdateWindow? _updateWindow;
    private BatteryIconFrame _lastFrame = BatteryIconFrame.Empty();
    private string _lastToolTip = "TinyBattery · 未选择设备";
    private string? _lastDisplaySignature;
    private bool _useDefaultIcon = true;

    public NotifyIconTrayPresenter(
        IBatteryIconService icons,
        IDeviceDefinitionStore definitions,
        IBatteryStateStore state,
        IUserSettingsStore settings,
        IIconThemeStore themes,
        UpdateState updateState,
        IServiceProvider services,
        ILogger<NotifyIconTrayPresenter> logger)
    {
        _icons = icons;
        _definitions = definitions;
        _state = state;
        _settings = settings;
        _themes = themes;
        _updateState = updateState;
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

        var about = new MenuItem { Header = "关于 TinyBattery" };
        about.Click += (_, _) => ShowAbout();

        var updates = new MenuItem { Header = "检查更新" };
        updates.Click += (_, _) => ShowUpdates();
        _updatesItem = updates;

        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        var menu = new ContextMenu();
        menu.Items.Add(_batteryItem);
        menu.Items.Add(_devicesMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(reload);
        menu.Items.Add(settings);
        menu.Items.Add(updates);
        menu.Items.Add(about);
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
        _updateState.Changed += OnUpdateStateChanged;
        ApplyUpdateHeader();
        RefreshFromState();
        _logger.LogInformation(
            "Tray icon started with theme '{ThemeId}' (created={Created}).",
            _icons.ThemeId,
            _tray.IsCreated);
    }

    public void SetBattery(BatteryIconState state, string toolTip) =>
        SetFrame(BatteryIconFrame.Single(state), toolTip);

    public void SetFrame(BatteryIconFrame frame, string toolTip)
    {
        _lastFrame = frame;
        _lastToolTip = toolTip;

        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(ApplyBattery);
            return;
        }

        ApplyBattery();
    }

    public void Refresh()
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

    private void ApplyBattery()
    {
        if (_tray is null)
        {
            return;
        }

        _tray.Icon = _useDefaultIcon
            ? _icons.GetDefaultIcon()
            : _icons.GetIcon(_lastFrame);
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
        var theme = _themes.Selected;
        var hasTheme = !string.Equals(
            theme.Id,
            IconThemeDescriptor.FallbackId,
            StringComparison.OrdinalIgnoreCase);
        var displayIds = _settings.Current.DisplayDeviceIds.Count > 0
            ? _settings.Current.DisplayDeviceIds
            : string.IsNullOrWhiteSpace(_settings.Current.PrimaryDeviceId)
                ? []
                : [_settings.Current.PrimaryDeviceId];

        var resolved = TrayDisplaySelector.Resolve(
            snapshots,
            displayIds,
            theme.SlotCount,
            hasTheme);
        var frameSlots = new List<BatteryIconSlotState>(theme.Slots.Count);
        for (var index = 0; index < theme.Slots.Count; index++)
        {
            var slot = theme.Slots[index];
            var snapshot = index < resolved.Slots.Count ? resolved.Slots[index] : null;
            var battery = snapshot?.Battery;
            frameSlots.Add(new BatteryIconSlotState(
                slot.Id,
                battery?.Percent,
                battery?.Charging is ChargingState.Charging,
                snapshot is not null && TrayDisplaySelector.IsReadable(snapshot)));
        }

        var frame = new BatteryIconFrame(frameSlots);
        var useDefaultIcon = resolved.UseDefaultIcon;
        var signature = $"{theme.Id}|{useDefaultIcon}|"
            + string.Join(
                ",",
                frame.Slots.Select(slot =>
                    $"{slot.SlotId}:{(slot.IsAvailable ? slot.Percent?.ToString() ?? "-" : "x")}:"
                    + $"{(slot.IsCharging ? 1 : 0)}"));

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
                    "Showing theme '{ThemeId}' with {Count} slot(s).",
                    theme.Id,
                    frame.Slots.Count);
            }
        }

        _useDefaultIcon = useDefaultIcon;

        if (useDefaultIcon)
        {
            SetFrame(
                frame,
                hasTheme
                    ? "TinyBattery · 未选择设备"
                    : "TinyBattery · 未安装主题");
        }
        else
        {
            var tooltip = BuildToolTip(theme, resolved.Slots);
            if (!hasTheme)
            {
                tooltip += " · 未安装主题";
            }

            SetFrame(frame, tooltip);
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

    private static string BuildToolTip(
        IconThemeDescriptor theme,
        IReadOnlyList<DeviceSnapshot?> slots)
    {
        var devices = slots
            .Where(snapshot => snapshot is not null && TrayDisplaySelector.IsReadable(snapshot))
            .Cast<DeviceSnapshot>()
            .ToArray();
        if (devices.Length == 0)
        {
            return "TinyBattery · 未选择设备";
        }

        if (devices.Length == 1)
        {
            return BuildToolTip(devices[0]);
        }

        var parts = new List<string>();
        var readableCount = 0;
        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var snapshot = slots[slotIndex];
            if (snapshot is null || !TrayDisplaySelector.IsReadable(snapshot))
            {
                continue;
            }

            readableCount++;
            if (parts.Count >= 3)
            {
                continue;
            }

            var label = slotIndex < theme.Slots.Count
                && !string.IsNullOrWhiteSpace(theme.Slots[slotIndex].Label)
                ? theme.Slots[slotIndex].Label!
                : snapshot.DisplayName;
            var percent = snapshot.Battery?.Percent is int value
                ? $" {value}%"
                : string.Empty;
            parts.Add($"{label}{percent}");
        }

        if (readableCount > parts.Count)
        {
            parts.Add($"+{readableCount - parts.Count}");
        }

        return $"TinyBattery · {string.Join(" · ", parts)}";
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

    private void OnUpdateStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            ApplyUpdateHeader();
        }
        else
        {
            dispatcher.BeginInvoke(ApplyUpdateHeader);
        }
    }

    private void ApplyUpdateHeader()
    {
        if (_updatesItem is null)
        {
            return;
        }

        var count = _updateState.AvailableCount;
        _updatesItem.Header = count > 0
            ? $"有 {count} 个更新…"
            : "检查更新";
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

    private void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = _services.GetRequiredService<AboutWindow>();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private void ShowUpdates()
    {
        if (_updateWindow is not null)
        {
            _updateWindow.Activate();
            return;
        }

        _updateWindow = _services.GetRequiredService<UpdateWindow>();
        _updateWindow.Closed += (_, _) => _updateWindow = null;
        _updateWindow.Show();
        _updateWindow.Activate();
    }

    public void Dispose()
    {
        _icons.ThemeChanged -= OnThemeChanged;
        _state.Updated -= OnStateUpdated;
        _updateState.Changed -= OnUpdateStateChanged;
        _settingsWindow?.Close();
        _aboutWindow?.Close();
        _updateWindow?.Close();
        _tray?.Dispose();
        _tray = null;
    }
}
