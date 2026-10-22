using System.IO;

namespace TinyBattery.Infrastructure.Plugins;

public static class PluginPathResolver
{
    public static IReadOnlyList<string> ExpandDeviceDefinitions(
        string pluginDirectory,
        string relativePath,
        out string? error)
    {
        error = null;
        var path = Resolve(pluginDirectory, relativePath, out error);
        if (path is null)
        {
            return [];
        }

        if (Directory.Exists(path))
        {
            return Directory
                .EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Device definition path '{relativePath}' must be a .json file or a folder.";
            return [];
        }

        return [path];
    }

    public static IReadOnlyList<string> ExpandThemeManifests(
        string pluginDirectory,
        string relativePath,
        out string? error)
    {
        error = null;
        var path = Resolve(pluginDirectory, relativePath, out error);
        if (path is null)
        {
            return [];
        }

        if (Directory.Exists(path))
        {
            return Directory
                .EnumerateFiles(path, "theme.json", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Theme path '{relativePath}' must be theme.json or a folder containing themes.";
            return [];
        }

        return [path];
    }

    public static string? Resolve(
        string pluginDirectory,
        string relativePath,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            error = "Plugin path is empty.";
            return null;
        }

        var root = Path.GetFullPath(pluginDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(pluginDirectory, relativePath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Plugin path '{relativePath}' escapes the plugin directory.";
            return null;
        }

        if (!File.Exists(full) && !Directory.Exists(full))
        {
            error = $"Plugin path '{relativePath}' was not found.";
            return null;
        }

        return full;
    }
}
