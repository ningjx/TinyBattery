using TinyBattery.Abstractions.Paths;

namespace TinyBattery.Infrastructure.Paths;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
        : this(null)
    {
    }

    public AppPaths(string? rootDirectory)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TinyBattery")
            : Path.GetFullPath(rootDirectory);

        DefinitionsDirectory = Path.Combine(RootDirectory, "devices");
        ThemesDirectory = Path.Combine(RootDirectory, "themes");
        PluginsDirectory = Path.Combine(RootDirectory, "plugins");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        SettingsFile = Path.Combine(RootDirectory, "settings.json");
        InstalledPackagesFile = Path.Combine(RootDirectory, "installed-packages.json");
        UpdateCacheDirectory = Path.Combine(RootDirectory, "updates");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
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

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DefinitionsDirectory);
        Directory.CreateDirectory(ThemesDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(UpdateCacheDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
