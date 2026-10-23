using System.IO;
using TinyBattery.Abstractions.Paths;

namespace TinyBattery.Core.Tests;

internal sealed class TestAppPaths : IAppPaths
{
    public TestAppPaths(string root)
    {
        RootDirectory = root;
        DefinitionsDirectory = Path.Combine(root, "devices");
        ThemesDirectory = Path.Combine(root, "themes");
        PluginsDirectory = Path.Combine(root, "plugins");
        LogsDirectory = Path.Combine(root, "logs");
        SettingsFile = Path.Combine(root, "settings.json");
        InstalledPackagesFile = Path.Combine(root, "installed-packages.json");
        UpdateCacheDirectory = Path.Combine(root, "updates");
        BackupsDirectory = Path.Combine(root, "backups");
    }

    public string RootDirectory { get; }

    public string DefinitionsDirectory { get; }

    public string ThemesDirectory { get; }

    public string PluginsDirectory { get; }

    public string LogsDirectory { get; }

    public string SettingsFile { get; }

    public string InstalledPackagesFile { get; }

    public string UpdateCacheDirectory { get; }

    public string BackupsDirectory { get; }
}
