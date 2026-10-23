using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Abstractions.Updating;
using TinyBattery.App.Updating;

namespace TinyBattery.App.Views;

public partial class UpdateWindow : Window
{
    private static readonly Brush GoodBrush = FrozenBrush("#2F9E64");
    private static readonly Brush DangerBrush = FrozenBrush("#C24B4B");
    private static readonly Brush SecondaryBrush = FrozenBrush("#666666");

    private readonly IUpdateService _updates;
    private readonly IUserSettingsStore _settings;
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;
    private readonly UpdateState _updateState;

    public UpdateWindow(
        IUpdateService updates,
        IUserSettingsStore settings,
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes,
        UpdateState updateState)
    {
        _updates = updates;
        _settings = settings;
        _definitions = definitions;
        _themes = themes;
        _updateState = updateState;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        FeedUrlBox.Text = _settings.Current.UpdateFeedUrl ?? string.Empty;
        StartupCheckBox.IsChecked = _settings.Current.CheckForUpdatesOnStartup;
        if (!string.IsNullOrWhiteSpace(_settings.Current.UpdateFeedUrl))
        {
            await CheckAsync();
        }
        else
        {
            SetStatus("请先填写 HTTPS 更新源地址。", SecondaryBrush);
        }
    }

    private async void SaveFeed_Click(object sender, RoutedEventArgs e)
    {
        var url = FeedUrlBox.Text.Trim();
        if (url.Length > 0
            && (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatus("更新源必须是 HTTPS 地址。", DangerBrush);
            return;
        }

        try
        {
            _settings.Current.UpdateFeedUrl = url.Length == 0 ? null : url;
            await _settings.SaveAsync(_settings.Current, CancellationToken.None);
            SetStatus("更新源已保存。", GoodBrush);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, DangerBrush);
        }
    }

    private async void StartupCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Current.CheckForUpdatesOnStartup = StartupCheckBox.IsChecked == true;
            await _settings.SaveAsync(_settings.Current, CancellationToken.None);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, DangerBrush);
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        try
        {
            SetStatus("正在检查更新…", SecondaryBrush);
            var result = await _updates.CheckAsync(CancellationToken.None);
            _updateState.Set(result.Updates.Count);
            UpdatesList.ItemsSource = result.Updates
                .Select(update => new UpdateRow(
                    update,
                    update.Package.Name,
                    $"{update.CurrentVersion} → {update.Package.Version}",
                    BuildDetail(update),
                    update.IsCompatible ? GoodBrush : DangerBrush))
                .ToArray();
            InstallButton.IsEnabled = result.Updates.Any(update => update.IsCompatible);
            SetStatus(result.Message, result.Success ? GoodBrush : DangerBrush);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, DangerBrush);
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (UpdatesList.SelectedItem is not UpdateRow row)
        {
            SetStatus("请先选择一个更新。", DangerBrush);
            return;
        }

        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        try
        {
            SetStatus($"正在安装 {row.Update.Package.Name}…", SecondaryBrush);
            var result = await _updates.InstallAsync(
                row.Update,
                CancellationToken.None);
            if (result.Success)
            {
                await _definitions.ReloadAsync(CancellationToken.None);
                await _themes.ReloadAsync(CancellationToken.None);
            }

            SetStatus(result.Message, result.Success ? GoodBrush : DangerBrush);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, DangerBrush);
        }
        finally
        {
            CheckButton.IsEnabled = true;
            await CheckAsync();
        }
    }

    private static string BuildDetail(AvailableUpdate update)
    {
        if (!update.IsCompatible)
        {
            return "此更新需要更高版本的 TinyBattery。";
        }

        var changes = update.Changes
            .Select(change =>
                $"{change.Id} {change.CurrentVersion ?? "未安装"} → {change.NewVersion}")
            .ToArray();
        var detail = changes.Length == 0
            ? update.Package.Notes ?? "包含新的内容。"
            : string.Join("；", changes);
        if (!string.IsNullOrWhiteSpace(update.Package.Notes))
        {
            detail += " · " + update.Package.Notes;
        }

        return detail;
    }

    private void SetStatus(string message, Brush brush)
    {
        StatusText.Text = message;
        StatusText.Foreground = brush;
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

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static Brush FrozenBrush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private sealed record UpdateRow(
        AvailableUpdate Update,
        string Title,
        string VersionText,
        string Detail,
        Brush Accent);
}
