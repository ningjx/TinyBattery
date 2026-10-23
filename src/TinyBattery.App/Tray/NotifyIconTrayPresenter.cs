using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly IBatteryStateStore _state;
    private readonly IUserSettingsStore _settings;
    private readonly IIconThemeStore _themes;
    private readonly UpdateState _updateState;
    private readonly IServiceProvider _services;
    private readonly ILogger<NotifyIconTrayPresenter> _logger;

    private TaskbarIcon? _tray;
    private TextBlock? _batteryStatusText;
    private MenuItem? _themeMenu;
    private MenuItem? _displayMenu;
    private MenuItem? _aboutItem;
    private Style? _menuItemStyle;
    private Style? _statusItemStyle;
    private Style? _separatorStyle;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private BatteryIconFrame _lastFrame = BatteryIconFrame.Empty();
    private string _lastToolTip = "TinyBattery · 未选择设备";
    private string? _lastDisplaySignature;
    private bool _useDefaultIcon = true;

    public NotifyIconTrayPresenter(
        IBatteryIconService icons,
        IBatteryStateStore state,
        IUserSettingsStore settings,
        IIconThemeStore themes,
        UpdateState updateState,
        IServiceProvider services,
        ILogger<NotifyIconTrayPresenter> logger)
    {
        _icons = icons;
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

        _menuItemStyle = Application.Current.TryFindResource("TrayMenuItem") as Style;
        _statusItemStyle = Application.Current.TryFindResource("TrayStatusMenuItem") as Style;
        _separatorStyle = Application.Current.TryFindResource("TraySeparator") as Style;

        var batteryItem = new MenuItem
        {
            Header = BuildStatusHeader(),
            IsEnabled = false,
            Style = _statusItemStyle,
        };
        _themeMenu = NewMenuItem("主题");
        _displayMenu = NewMenuItem("显示设备");

        var settings = NewMenuItem("设置");
        settings.Click += (_, _) => ShowSettings();

        var about = NewMenuItem("关于与更新");
        about.Click += (_, _) => ShowAbout();
        _aboutItem = about;

        var exit = NewMenuItem("退出");
        exit.Click += (_, _) => Application.Current.Shutdown();

        var menu = new ContextMenu
        {
            Style = Application.Current.TryFindResource("TrayContextMenu") as Style,
        };
        menu.Items.Add(batteryItem);
        menu.Items.Add(_themeMenu);
        menu.Items.Add(_displayMenu);
        menu.Items.Add(NewSeparator());
        menu.Items.Add(settings);
        menu.Items.Add(about);
        menu.Items.Add(NewSeparator());
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
        if (_batteryStatusText is not null)
        {
            _batteryStatusText.Text = _lastToolTip;
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

        RebuildDynamicMenus(snapshots, theme, resolved);
    }

    private void RebuildDynamicMenus(
        IReadOnlyList<DeviceSnapshot> snapshots,
        IconThemeDescriptor theme,
        TrayDisplayResult resolved)
    {
        if (_themeMenu is null || _displayMenu is null)
        {
            return;
        }

        _themeMenu.Items.Clear();
        _displayMenu.Items.Clear();

        var svgThemes = _themes.Themes
            .Where(candidate => candidate.Kind == IconThemeKind.Svg)
            .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _themeMenu.Header = svgThemes.Length > 0
            ? $"主题 · {(theme.Kind == IconThemeKind.Svg ? theme.DisplayName : "未选择")}"
            : "主题";
        if (svgThemes.Length == 0)
        {
            _themeMenu.Items.Add(DisabledItem("未安装 SVG 主题"));
        }
        else
        {
            foreach (var candidate in svgThemes)
            {
                var item = NewMenuItem(candidate.DisplayName);
                item.IsCheckable = true;
                item.IsChecked = string.Equals(
                    candidate.Id,
                    theme.Id,
                    StringComparison.OrdinalIgnoreCase);
                item.ToolTip = $"{candidate.SlotCount} 个槽位"
                    + (string.IsNullOrWhiteSpace(candidate.Version)
                        ? string.Empty
                        : $" · v{candidate.Version}");
                var themeId = candidate.Id;
                item.Click += async (_, _) => await SelectThemeAsync(themeId);
                _themeMenu.Items.Add(item);
            }
        }

        var hasSvgTheme = theme.Kind == IconThemeKind.Svg;
        _displayMenu.Header = hasSvgTheme && theme.SlotCount > 1
            ? "显示设备 · 多设备"
            : "显示设备";
        _displayMenu.IsEnabled = hasSvgTheme;
        if (!hasSvgTheme)
        {
            _displayMenu.Items.Add(DisabledItem("安装 SVG 主题后可配置"));
            return;
        }

        var displayIds = _settings.Current.DisplayDeviceIds;
        for (var index = 0; index < theme.Slots.Count; index++)
        {
            var slot = theme.Slots[index];
            var assigned = index < displayIds.Count ? displayIds[index] : null;
            if (theme.Slots.Count == 1)
            {
                AddSlotOptions(_displayMenu, index, assigned, snapshots);
                continue;
            }

            var slotMenu = NewMenuItem(SlotHeader(slot, assigned, resolved, index));
            AddSlotOptions(slotMenu, index, assigned, snapshots);
            _displayMenu.Items.Add(slotMenu);
        }
    }

    private void AddSlotOptions(
        ItemsControl parent,
        int slotIndex,
        string? assigned,
        IReadOnlyList<DeviceSnapshot> snapshots)
    {
        var auto = NewMenuItem("自动选择");
        auto.IsCheckable = true;
        auto.IsChecked = string.IsNullOrWhiteSpace(assigned);
        auto.Click += async (_, _) => await AssignSlotAsync(slotIndex, null);
        parent.Items.Add(auto);
        parent.Items.Add(NewSeparator());

        if (snapshots.Count == 0)
        {
            parent.Items.Add(DisabledItem("未发现设备"));
            return;
        }

        var matchFound = false;
        foreach (var snapshot in snapshots)
        {
            var isAssigned = string.Equals(
                assigned,
                snapshot.LogicalDeviceId,
                StringComparison.OrdinalIgnoreCase);
            matchFound |= isAssigned;
            var item = NewMenuItem(BuildDeviceOptionLabel(snapshot));
            item.IsCheckable = true;
            item.IsChecked = isAssigned;
            var deviceId = snapshot.LogicalDeviceId;
            item.Click += async (_, _) => await AssignSlotAsync(slotIndex, deviceId);
            parent.Items.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(assigned) && !matchFound)
        {
            var missing = NewMenuItem($"{assigned}（未连接）");
            missing.IsCheckable = true;
            missing.IsChecked = true;
            missing.IsEnabled = false;
            parent.Items.Add(missing);
        }
    }

    private string SlotHeader(
        IconThemeSlot slot,
        string? assigned,
        TrayDisplayResult resolved,
        int index)
    {
        var label = string.IsNullOrWhiteSpace(slot.Label)
            ? $"设备 {index + 1}"
            : slot.Label;
        var snapshot = index < resolved.Slots.Count ? resolved.Slots[index] : null;
        if (snapshot is null)
        {
            return $"{label} · 未选择";
        }

        return $"{label} · {BuildDeviceOptionLabel(snapshot)}";
    }

    private static string BuildDeviceOptionLabel(DeviceSnapshot snapshot)
    {
        var text = snapshot.DisplayName;
        if (snapshot.Battery?.Percent is int percent)
        {
            text += $" · {percent}%";
        }
        else if (snapshot.Battery?.VoltageMv is { } voltage)
        {
            text += $" · {voltage / 1000:0.00} V";
        }

        if (snapshot.Battery?.Charging is ChargingState.Charging)
        {
            text += " · 充电中";
        }
        else if (snapshot.Battery?.Charging is ChargingState.Full)
        {
            text += " · 已充满";
        }

        return text;
    }

    private async Task SelectThemeAsync(string themeId)
    {
        try
        {
            await _themes.SelectAsync(themeId, CancellationToken.None);
            RefreshFromState();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not select theme '{ThemeId}'.", themeId);
        }
    }

    private async Task AssignSlotAsync(int slotIndex, string? deviceId)
    {
        try
        {
            var ids = _settings.Current.DisplayDeviceIds;
            while (ids.Count <= slotIndex)
            {
                ids.Add(string.Empty);
            }

            ids[slotIndex] = deviceId ?? string.Empty;
            _settings.Current.PrimaryDeviceId = ids.FirstOrDefault(
                id => !string.IsNullOrWhiteSpace(id));
            await _settings.SaveAsync(_settings.Current, CancellationToken.None);
            RefreshFromState();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not assign slot {SlotIndex}.",
                slotIndex);
        }
    }

    private MenuItem NewMenuItem(string header) =>
        new()
        {
            Header = header,
            Style = _menuItemStyle,
        };

    private MenuItem DisabledItem(string header)
    {
        var item = NewMenuItem(header);
        item.IsEnabled = false;
        return item;
    }

    private Separator NewSeparator() => new() { Style = _separatorStyle };

    private StackPanel BuildStatusHeader()
    {
        var primary = Application.Current.TryFindResource("TextPrimaryBrush") as Brush
            ?? Brushes.Black;
        var secondary = Application.Current.TryFindResource("TextSecondaryBrush") as Brush
            ?? Brushes.Gray;
        var panel = new StackPanel { Margin = new Thickness(0, 1, 0, 1) };
        panel.Children.Add(new TextBlock
        {
            Text = "TinyBattery",
            FontSize = 11,
            Foreground = secondary,
        });
        _batteryStatusText = new TextBlock
        {
            Text = _lastToolTip,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = primary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280,
        };
        panel.Children.Add(_batteryStatusText);
        return panel;
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
        if (_aboutItem is null)
        {
            return;
        }

        var count = _updateState.AvailableCount;
        _aboutItem.Header = count > 0
            ? $"关于与更新 · {count} 个更新"
            : "关于与更新";
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

    public void Dispose()
    {
        _icons.ThemeChanged -= OnThemeChanged;
        _state.Updated -= OnStateUpdated;
        _updateState.Changed -= OnUpdateStateChanged;
        _settingsWindow?.Close();
        _aboutWindow?.Close();
        _tray?.Dispose();
        _tray = null;
    }
}
