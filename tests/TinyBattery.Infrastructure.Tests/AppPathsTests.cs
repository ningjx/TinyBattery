using System.IO;
using TinyBattery.Infrastructure.Paths;

namespace TinyBattery.Infrastructure.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Paths_AreNestedUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var paths = new AppPaths(root);

        Assert.Equal(Path.GetFullPath(root), paths.RootDirectory);
        Assert.Equal(Path.Combine(root, "devices"), paths.DefinitionsDirectory);
        Assert.Equal(Path.Combine(root, "themes"), paths.ThemesDirectory);
        Assert.Equal(Path.Combine(root, "plugins"), paths.PluginsDirectory);
        Assert.Equal(Path.Combine(root, "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(root, "settings.json"), paths.SettingsFile);
    }
}
