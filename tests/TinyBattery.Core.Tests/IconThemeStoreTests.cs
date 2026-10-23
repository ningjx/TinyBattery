using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Core.Themes;

namespace TinyBattery.Core.Tests;

public sealed class IconThemeStoreTests
{
    [Fact]
    public async Task Fallback_IsAlwaysAvailable()
    {
        var root = CreateRoot();
        try
        {
            var store = CreateStore(root, new FakeUserSettingsStore());
            await store.ReloadAsync(CancellationToken.None);

            Assert.Equal("fallback", store.Selected.Id);
            Assert.Contains(store.Themes, theme => theme.Id == "fallback");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task UserTheme_IsLoadedAndSelectedFromSettings()
    {
        var root = CreateRoot();
        try
        {
            WriteClassicTheme(Path.Combine(root, "themes"), "blue", "Blue");
            var settings = new FakeUserSettingsStore
            {
                Current = new UserSettings { BatteryThemeId = "blue" },
            };
            var store = CreateStore(root, settings);

            await store.ReloadAsync(CancellationToken.None);

            Assert.Equal("blue", store.Selected.Id);
            Assert.Contains(store.Themes, theme => theme.Id == "blue");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PluginThemeManifest_IsLoaded()
    {
        var root = CreateRoot();
        try
        {
            var pluginThemes = Path.Combine(root, "plugins", "classic", "themes");
            WriteClassicTheme(pluginThemes, "classic", "Classic");
            var catalog = new FakePluginAssetCatalog();
            catalog.ThemeManifests.Add(
                Path.Combine(pluginThemes, "classic", "theme.json"));
            var settings = new FakeUserSettingsStore
            {
                Current = new UserSettings { BatteryThemeId = "classic" },
            };
            var store = CreateStore(root, settings, catalog);

            await store.ReloadAsync(CancellationToken.None);

            Assert.Equal("classic", store.Selected.Id);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SelectTheme_PersistsToSettings()
    {
        var root = CreateRoot();
        try
        {
            WriteClassicTheme(Path.Combine(root, "themes"), "blue", "Blue");
            var settings = new FakeUserSettingsStore();
            var store = CreateStore(root, settings);
            await store.ReloadAsync(CancellationToken.None);

            await store.SelectAsync("blue", CancellationToken.None);

            Assert.Equal("blue", settings.Current.BatteryThemeId);
            Assert.Equal("blue", store.Selected.Id);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MissingSelectedTheme_FallsBackToFallback()
    {
        var root = CreateRoot();
        try
        {
            var settings = new FakeUserSettingsStore
            {
                Current = new UserSettings { BatteryThemeId = "missing" },
            };
            var store = CreateStore(root, settings);

            await store.ReloadAsync(CancellationToken.None);

            Assert.Equal("fallback", store.Selected.Id);
            Assert.Contains(store.Diagnostics, diagnostic =>
                diagnostic.Errors.Any(error => error.Contains("falling back", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static JsonIconThemeStore CreateStore(
        string root,
        FakeUserSettingsStore settings,
        FakePluginAssetCatalog? catalog = null) =>
        new(
            new TestAppPaths(root),
            catalog ?? new FakePluginAssetCatalog(),
            settings,
            new IconThemeLoader(
                new IconThemeValidator(),
                new FakeImageAssetValidator()),
            NullLogger<JsonIconThemeStore>.Instance);

    private static void WriteClassicTheme(string themesRoot, string id, string name)
    {
        var directory = Path.Combine(themesRoot, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "icon.svg"),
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
              <defs>
                <rect id="tb-fill" x="4" y="4" width="24" height="24" />
              </defs>
            </svg>
            """);
        File.WriteAllText(
            Path.Combine(directory, "theme.json"),
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "{{name}}",
              "kind": "svg",
              "slotCount": 1,
              "render": {
                "svg": "icon.svg",
                "slots": [
                  { "id": "main", "mode": "rect", "target": "tb-fill" }
                ]
              }
            }
            """);
    }

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "TinyBattery.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
