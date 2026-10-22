using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Infrastructure.Plugins;

namespace TinyBattery.Infrastructure.Tests;

public sealed class PluginLoaderTests
{
    [Fact]
    public async Task DataOnlyPlugin_LoadsDeviceAndThemePaths()
    {
        var root = CreateRoot();
        try
        {
            var plugin = Path.Combine(root, "plugins", "my-plugin");
            Directory.CreateDirectory(Path.Combine(plugin, "devices"));
            Directory.CreateDirectory(Path.Combine(plugin, "themes", "my-theme"));
            await File.WriteAllTextAsync(
                Path.Combine(plugin, "plugin.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "my-plugin",
                  "name": "My Plugin",
                  "version": "1.0.0",
                  "devices": ["devices"],
                  "themes": ["themes"]
                }
                """);
            await File.WriteAllTextAsync(
                Path.Combine(plugin, "devices", "device.json"),
                "{}");
            await File.WriteAllTextAsync(
                Path.Combine(plugin, "themes", "my-theme", "theme.json"),
                "{}");

            var catalog = new PluginAssetCatalog();
            var loader = CreateLoader(root, catalog);
            await loader.LoadAsync(CancellationToken.None);

            Assert.Contains(catalog.DeviceDefinitionPaths, path =>
                path.EndsWith("device.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(catalog.ThemeManifestPaths, path =>
                path.EndsWith("theme.json", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(catalog.Diagnostics);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task InvalidManifest_IsReported()
    {
        var root = CreateRoot();
        try
        {
            var plugin = Path.Combine(root, "plugins", "broken");
            Directory.CreateDirectory(plugin);
            await File.WriteAllTextAsync(
                Path.Combine(plugin, "plugin.json"),
                """{ "schemaVersion": 1, "id": "broken" }""");

            var catalog = new PluginAssetCatalog();
            var loader = CreateLoader(root, catalog);
            await loader.LoadAsync(CancellationToken.None);

            Assert.NotEmpty(catalog.Diagnostics);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task EscapingPath_IsReported()
    {
        var root = CreateRoot();
        try
        {
            var plugin = Path.Combine(root, "plugins", "escaping");
            Directory.CreateDirectory(plugin);
            await File.WriteAllTextAsync(
                Path.Combine(plugin, "plugin.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "escaping",
                  "name": "Escaping",
                  "version": "1.0.0",
                  "devices": ["../outside.json"]
                }
                """);

            var catalog = new PluginAssetCatalog();
            var loader = CreateLoader(root, catalog);
            await loader.LoadAsync(CancellationToken.None);

            Assert.Contains(catalog.Diagnostics, diagnostic =>
                diagnostic.Message.Contains("escapes", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static PluginLoader CreateLoader(string root, PluginAssetCatalog catalog) =>
        new(
            new TestPaths(root),
            catalog,
            new FakeProtocolRegistry(),
            new PluginManifestValidator(),
            NullLoggerFactory.Instance,
            NullLogger<PluginLoader>.Instance);

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "TinyBattery.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeProtocolRegistry : IProtocolRegistry
    {
        public IBatteryProtocol Resolve(string kind) =>
            throw new NotSupportedException();

        public bool TryResolve(string kind, out IBatteryProtocol? protocol)
        {
            protocol = null;
            return false;
        }

        public void Register(IBatteryProtocol protocol)
        {
        }
    }

    private sealed class TestPaths : IAppPaths
    {
        public TestPaths(string root)
        {
            RootDirectory = root;
            DefinitionsDirectory = Path.Combine(root, "devices");
            ThemesDirectory = Path.Combine(root, "themes");
            PluginsDirectory = Path.Combine(root, "plugins");
            LogsDirectory = Path.Combine(root, "logs");
            SettingsFile = Path.Combine(root, "settings.json");
        }

        public string RootDirectory { get; }

        public string DefinitionsDirectory { get; }

        public string ThemesDirectory { get; }

        public string PluginsDirectory { get; }

        public string LogsDirectory { get; }

        public string SettingsFile { get; }
    }
}
