using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;

namespace TinyBattery.App.Views;

public partial class SettingsWindow : Window
{
    private readonly IDeviceDefinitionStore _definitions;
    private readonly IIconThemeStore _themes;
    private readonly IPluginAssetCatalog _plugins;
    private readonly IAppPaths _paths;
    private readonly IBatteryIconService _icons;
    private readonly IDeviceDefinitionImporter _deviceImporter;
    private readonly IIconThemeImporter _themeImporter;
    private readonly IPackageImporter _packageImporter;
    private bool _updating;

    public SettingsWindow(
        IDeviceDefinitionStore definitions,
        IIconThemeStore themes,
        IPluginAssetCatalog plugins,
        IAppPaths paths,
        IBatteryIconService icons,
        IDeviceDefinitionImporter deviceImporter,
        IIconThemeImporter themeImporter,
        IPackageImporter packageImporter)
    {
        _definitions = definitions;
        _themes = themes;
        _plugins = plugins;
        _paths = paths;
        _icons = icons;
        _deviceImporter = deviceImporter;
        _themeImporter = themeImporter;
        _packageImporter = packageImporter;
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _updating = true;
        try
        {
            await _definitions.ReloadAsync(CancellationToken.None);
            await _themes.ReloadAsync(CancellationToken.None);

            DefinitionsList.ItemsSource = _definitions.Definitions
                .Select(definition =>
                    $"{definition.DisplayName}  [{definition.Id}]  {definition.Protocol.Kind}")
                .ToArray();

            ThemeComboBox.ItemsSource = _themes.Themes;
            ThemeComboBox.SelectedItem = _themes.Selected;
            ApplyThemePreview();
            UpdateDiagnostics();
        }
        catch (Exception exception)
        {
            DiagnosticsText.Text = exception.Message;
        }
        finally
        {
            _updating = false;
        }
    }

    private async void ThemeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_updating || ThemeComboBox.SelectedItem is not IconThemeDescriptor theme)
        {
            return;
        }

        try
        {
            await _themes.SelectAsync(theme.Id, CancellationToken.None);
            ApplyThemePreview();
            UpdateDiagnostics();
        }
        catch (Exception exception)
        {
            DiagnosticsText.Text = exception.Message;
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
            DiagnosticsText.Text = exception.Message;
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
        DiagnosticsText.Text = string.Join(Environment.NewLine, lines);
    }

    private void ApplyThemePreview()
    {
        var theme = _themes.Selected;
        ThemePreview.Source = theme.Kind == IconThemeKind.ImageSet
            && !string.IsNullOrWhiteSpace(theme.PreviewPath)
            ? LoadImage(theme.PreviewPath)
            : RenderThemePreview(theme);
        Icon = LoadImage(theme.AppIconPath);
    }

    private ImageSource? RenderThemePreview(IconThemeDescriptor theme)
    {
        try
        {
            const int previewSize = 80;
            var icon = string.Equals(
                    theme.Id,
                    IconThemeDescriptor.FallbackId,
                    StringComparison.OrdinalIgnoreCase)
                ? _icons.GetDefaultIcon(previewSize)
                : _icons.GetIcon(new BatteryIconState(80), previewSize);
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception exception)
        {
            DiagnosticsText.Text = exception.Message;
            return null;
        }
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

        DiagnosticsText.Text = lines.Count == 0
            ? "配置全部有效。"
            : string.Join(Environment.NewLine, lines);
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }
}
