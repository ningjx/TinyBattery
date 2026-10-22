namespace TinyBattery.Abstractions.Paths;

public interface IAppPaths
{
    string RootDirectory { get; }

    string DefinitionsDirectory { get; }

    string ThemesDirectory { get; }

    string PluginsDirectory { get; }

    string LogsDirectory { get; }

    string SettingsFile { get; }
}
