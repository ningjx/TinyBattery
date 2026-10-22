using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using TinyBattery.Abstractions.Paths;

namespace TinyBattery.App.Views;

public partial class AboutWindow : Window
{
    private readonly IAppPaths _paths;

    public AboutWindow(IAppPaths paths)
    {
        _paths = paths;
        InitializeComponent();

        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? "版本未知"
            : $"版本 {version.Major}.{version.Minor}.{version.Build}";
        ConfigPathText.Text = _paths.RootDirectory;
    }

    private void OnConfigFolderClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.RootDirectory,
            UseShellExecute = true,
        });
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
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
}
