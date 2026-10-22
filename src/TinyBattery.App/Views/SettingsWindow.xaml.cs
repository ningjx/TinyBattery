using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.State;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;
using TinyBattery.App.Tray;

namespace TinyBattery.App.Views;

public partial class SettingsWindow : Window
{
    private static readonly Brush GoodBrush = FrozenBrush("#2F9E64");
    private static readonly Brush WarningBrush = FrozenBrush("#C08A1E");
    private static readonly Brush DangerBrush = FrozenBrush("#C24B4B");
    private static readonly Brush MutedBrush = FrozenBrush("#8C8C8C");

    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;
    private readonly IPluginAssetCatalog _plugins;
    private readonly IAppPaths _paths;
    private readonly IBatteryIconService _icons;
    private readonly IBatteryIconRendererFactory _renderers;
    private readonly IBatteryStateStore _state;
    private readonly IUserSettingsStore _settings;
    private readonly ITrayPresenter _tray;
    private readonly IDeviceDefinitionImporter _deviceImporter;
    private readonly IIconThemeImporter _themeImporter;
    private readonly IPackageImporter _packageImporter;
    private readonly IServiceProvider _services;

    private AboutWindow? _aboutWindow;
    private bool _updating;
    private bool _subscribed;

    public SettingsWindow(
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes,
        IPluginAssetCatalog plugins,
        IAppPaths paths,
        IBatteryIconService icons,
        IBatteryIconRendererFactory renderers,
        IBatteryStateStore state,
        IUserSettingsStore settings,
        ITrayPresenter tray,
        IDeviceDefinitionImporter deviceImporter,
        IIconThemeImporter themeImporter,
        IPackageImporter packageImporter,
        IServiceProvider services)
    {
        _definitions = definitions;
        _themes = themes;
        _plugins = plugins;
        _paths = paths;
        _icons = icons;
        _renderers = renderers;
        _state = state;
        _settings = settings;
        _tray = tray;
        _deviceImporter = deviceImporter;
        _themeImporter = themeImporter;
        _packageImporter = packageImporter;
        _services = services;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            _state.Updated += OnStateUpdated;
            _subscribed = true;
        }

        await RefreshAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_subscribed)
        {
            _state.Updated -= OnStateUpdated;
            _subscribed = false;
        }
    }

    private async Task RefreshAsync()
    {
        _updating = true;
        try
        {
            await _definitions.ReloadAsync(CancellationToken.None);
            await _themes.ReloadAsync(CancellationToken.None);
            BuildThemes();
            BuildDevices();
            BuildDefinitions();
            UpdateDiagnostics();
        }
        catch (Exception exception)
        {
            ShowStatus([exception.Message], success: false);
        }
        finally
        {
            _updating = false;
        }
    }

    private void BuildThemes()
    {
        var rows = _themes.Themes
            .Select(theme => CreateThemeRow(theme))
            .ToArray();
        ThemeList.ItemsSource = rows;

        var selected = rows.FirstOrDefault(row => string.Equals(
                row.Descriptor.Id,
                _themes.Selected.Id,
                StringComparison.OrdinalIgnoreCase))
            ?? rows.FirstOrDefault();
        ThemeList.SelectedItem = selected;

        if (selected is not null)
        {
            ApplyThemeDetail(selected.Descriptor);
        }
    }

    private ThemeRow CreateThemeRow(IconThemeDescriptor theme)
    {
        var isFallback = string.Equals(
            theme.Id,
            IconThemeDescriptor.FallbackId,
            StringComparison.OrdinalIgnoreCase);
        var name = isFallback ? "程序默认图标" : theme.DisplayName;
        var subtitle = isFallback
            ? "未安装可用主题时自动使用"
            : $"{KindLabel(theme.Kind)}"
                + (string.IsNullOrWhiteSpace(theme.Author)
                    ? string.Empty
                    : $" · {theme.Author}");
        return new ThemeRow(
            theme,
            name,
            subtitle,
            RenderThemePreview(theme, 32));
    }

    private void ApplyThemeDetail(IconThemeDescriptor theme)
    {
        var isFallback = string.Equals(
            theme.Id,
            IconThemeDescriptor.FallbackId,
            StringComparison.OrdinalIgnoreCase);

        ThemePreview.Source = RenderThemePreview(theme, 80);
        HeaderIcon.Source = RenderThemePreview(theme, 32);
        ThemeNameText.Text = isFallback ? "程序默认图标" : theme.DisplayName;
        ThemeDescriptionText.Text = isFallback
            ? "没有可用的图标主题时，托盘显示这个内置图标。"
            : theme.Description ?? "这个主题没有提供描述。";

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(theme.Version))
        {
            meta.Add($"版本 {theme.Version}");
        }

        if (!string.IsNullOrWhiteSpace(theme.Author))
        {
            meta.Add($"作者 {theme.Author}");
        }

        meta.Add(isFallback ? "程序内置" : KindLabel(theme.Kind));
        ThemeMetaText.Text = string.Join(" · ", meta);

        var appIcon = LoadImage(theme.AppIconPath);
        Icon = appIcon ?? FromIcon(_icons.GetDefaultIcon(32));
    }

    private ImageSource? RenderThemePreview(IconThemeDescriptor theme, int size)
    {
        if (theme.Kind == IconThemeKind.ImageSet
            && !string.IsNullOrWhiteSpace(theme.PreviewPath))
        {
            var preview = LoadImage(theme.PreviewPath);
            if (preview is not null)
            {
                return preview;
            }
        }

        if (string.Equals(
                theme.Id,
                IconThemeDescriptor.FallbackId,
                StringComparison.OrdinalIgnoreCase))
        {
            return FromIcon(_icons.GetDefaultIcon(size));
        }

        try
        {
            using var renderer = _renderers.Create(theme);
            using var icon = renderer.Render(new BatteryIconState(80), size);
            return FromIcon(icon);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void BuildDevices()
    {
        var snapshots = _state.Snapshot()
            .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<DeviceRow>
        {
            new(
                null,
                "自动选择",
                "跟随当前电量最低的设备",
                string.Empty,
                Brushes.Transparent),
        };
        rows.AddRange(snapshots.Select(CreateDeviceRow));
        DeviceList.ItemsSource = rows;

        var primaryId = _settings.Current.PrimaryDeviceId;
        var selected = string.IsNullOrWhiteSpace(primaryId)
            ? rows[0]
            : rows.FirstOrDefault(row => string.Equals(
                row.DeviceId,
                primaryId,
                StringComparison.OrdinalIgnoreCase));
        DeviceList.SelectedItem = selected;
        DeviceCountText.Text = snapshots.Length switch
        {
            0 => "未检测到设备",
            _ when selected is null => "已选设备未连接",
            _ => $"{snapshots.Length} 个设备",
        };
    }

    private static DeviceRow CreateDeviceRow(DeviceSnapshot snapshot)
    {
        var battery = snapshot.Battery;
        var percent = battery?.Percent;
        var percentText = percent is int value
            ? $"{value}%"
            : battery?.VoltageMv is double voltage
                ? $"{voltage / 1000:0.00} V"
                : "--";
        var accent = percent switch
        {
            >= 60 => GoodBrush,
            >= 20 => WarningBrush,
            not null => DangerBrush,
            _ => MutedBrush,
        };

        var details = new List<string> { StateLabel(snapshot.State) };
        details.AddRange(battery?.Charging switch
        {
            ChargingState.Charging => ["充电中"],
            ChargingState.Full => ["已充满"],
            ChargingState.Error => ["电池异常"],
            _ => Array.Empty<string>(),
        });
        if (snapshot.IsStale)
        {
            details.Add("数据未更新");
        }

        return new DeviceRow(
            snapshot.LogicalDeviceId,
            snapshot.DisplayName,
            string.Join(" · ", details),
            percentText,
            accent);
    }

    private void BuildDefinitions()
    {
        DefinitionsList.ItemsSource = _definitions.Definitions
            .Select(definition => new DefinitionRow(
                definition.DisplayName,
                $"{definition.Id} · {definition.Protocol.Kind}",
                definition.Enabled ? "启用" : "停用"))
            .ToArray();
        DefinitionCountText.Text = $"{_definitions.Definitions.Count} 个";
    }

    private async void ThemeList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updating || ThemeList.SelectedItem is not ThemeRow row)
        {
            return;
        }

        try
        {
            await _themes.SelectAsync(row.Descriptor.Id, CancellationToken.None);
            ApplyThemeDetail(row.Descriptor);
            UpdateDiagnostics();
        }
        catch (Exception exception)
        {
            ShowStatus([exception.Message], success: false);
        }
    }

    private async void DeviceList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updating || DeviceList.SelectedItem is not DeviceRow row)
        {
            return;
        }

        var current = _settings.Current.PrimaryDeviceId;
        if (string.Equals(current, row.DeviceId, StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrEmpty(current) && row.DeviceId is null))
        {
            return;
        }

        try
        {
            _settings.Current.PrimaryDeviceId = row.DeviceId;
            await _settings.SaveAsync(_settings.Current, CancellationToken.None);
            _tray.Refresh();
        }
        catch (Exception exception)
        {
            ShowStatus([exception.Message], success: false);
        }
    }

    private void OnStateUpdated(object? sender, DeviceSnapshot snapshot)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            RefreshDevicesQuietly();
        }
        else
        {
            dispatcher.BeginInvoke(RefreshDevicesQuietly);
        }
    }

    private void RefreshDevicesQuietly()
    {
        var wasUpdating = _updating;
        _updating = true;
        try
        {
            BuildDevices();
        }
        finally
        {
            _updating = wasUpdating;
        }
    }

    private void OpenPluginFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_paths.PluginsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _paths.PluginsDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            ShowStatus([exception.Message], success: false);
        }
    }

    private async void ImportDevice_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入设备定义",
            Filter = "设备定义 (*.json)|*.json|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await _deviceImporter.ImportAsync(
            dialog.FileName,
            new ImportOptions(OverwriteCheckBox.IsChecked == true),
            CancellationToken.None);
        await ShowImportResultAsync(result);
    }

    private async void ImportThemeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "导入图标主题文件夹",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await _themeImporter.ImportAsync(
            dialog.FolderName,
            new ImportOptions(OverwriteCheckBox.IsChecked == true),
            CancellationToken.None);
        await ShowImportResultAsync(result);
    }

    private async void ImportThemeArchive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入图标主题包",
            Filter = "图标主题 (*.tbtheme;*.zip)|*.tbtheme;*.zip|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await _themeImporter.ImportAsync(
            dialog.FileName,
            new ImportOptions(OverwriteCheckBox.IsChecked == true),
            CancellationToken.None);
        await ShowImportResultAsync(result);
    }

    private async void ImportPackage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入扩展包",
            Filter = "扩展包 (*.tbpack;*.zip)|*.tbpack;*.zip|所有文件 (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var result = await _packageImporter.ImportAsync(
            dialog.FileName,
            new ImportOptions(OverwriteCheckBox.IsChecked == true),
            CancellationToken.None);
        await ShowImportResultAsync(result);
    }

    private async Task ShowImportResultAsync(ImportResult result)
    {
        await RefreshAsync();
        var lines = new List<string>
        {
            result.Success ? "导入成功：" : "导入失败：",
        };
        lines.AddRange(result.InstalledPaths);
        lines.AddRange(result.Diagnostics);
        ShowStatus(lines, result.Success);
    }

    private void UpdateDiagnostics()
    {
        var lines = new List<string>();
        lines.AddRange(_plugins.Diagnostics.Select(diagnostic =>
            $"插件 {diagnostic.Source}: {diagnostic.Message}"));
        lines.AddRange(_definitions.Diagnostics.Select(diagnostic =>
            $"{diagnostic.Source}: {string.Join("; ", diagnostic.Errors)}"));
        lines.AddRange(_themes.Diagnostics.Select(diagnostic =>
            $"{diagnostic.Source}: {string.Join("; ", diagnostic.Errors)}"));

        if (_plugins.DeviceDefinitionPaths.Count == 0
            && _plugins.ThemeManifestPaths.Count == 0
            && _plugins.ThemePlugins.Count == 0
            && _plugins.Protocols.Count == 0)
        {
            lines.Add("未加载任何插件。请把插件文件夹放入 plugins 目录后重启应用。");
        }

        if (lines.Count == 0)
        {
            DiagnosticsText.Foreground = GoodBrush;
            DiagnosticsText.Text = "配置全部有效。";
            return;
        }

        DiagnosticsText.Foreground = DangerBrush;
        DiagnosticsText.Text = string.Join(Environment.NewLine, lines);
    }

    private void ShowStatus(IEnumerable<string> lines, bool success)
    {
        DiagnosticsText.Foreground = success ? GoodBrush : DangerBrush;
        DiagnosticsText.Text = string.Join(Environment.NewLine, lines);
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        if (_aboutWindow is null)
        {
            _aboutWindow = _services.GetRequiredService<AboutWindow>();
            _aboutWindow.Owner = this;
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
            _aboutWindow.Show();
        }

        _aboutWindow.Activate();
    }

    private void OnTitleBarMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private static string StateLabel(DeviceConnectionState state) => state switch
    {
        DeviceConnectionState.Discovered => "已发现",
        DeviceConnectionState.Opening => "正在连接",
        DeviceConnectionState.Connected => "已连接",
        DeviceConnectionState.Asleep => "休眠",
        DeviceConnectionState.Unreachable => "未连接",
        DeviceConnectionState.AccessDenied => "被其他程序占用",
        DeviceConnectionState.Faulted => "读取失败",
        DeviceConnectionState.Disabled => "已禁用",
        _ => "未知状态",
    };

    private static string KindLabel(IconThemeKind kind) => kind switch
    {
        IconThemeKind.Classic => "经典矢量",
        IconThemeKind.ImageSet => "图片集",
        IconThemeKind.Plugin => "插件主题",
        _ => "未知类型",
    };

    private static ImageSource FromIcon(System.Drawing.Icon icon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    private static ImageSource? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Brush FrozenBrush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private sealed record ThemeRow(
        IconThemeDescriptor Descriptor,
        string Name,
        string Subtitle,
        ImageSource? Preview);

    private sealed record DeviceRow(
        string? DeviceId,
        string Title,
        string Detail,
        string Percent,
        Brush Accent);

    private sealed record DefinitionRow(
        string Name,
        string Detail,
        string Kind);
}
