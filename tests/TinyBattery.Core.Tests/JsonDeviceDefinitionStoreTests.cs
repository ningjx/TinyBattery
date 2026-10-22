using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Core.Registry;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Tests;

public sealed class JsonDeviceDefinitionStoreTests
{
    [Fact]
    public async Task LoadsPluginDefinitions()
    {
        await WithStoreAsync(async (store, _, catalog) =>
        {
            catalog.DeviceDefinitions.Add(LofreeFixture());

            await store.ReloadAsync(CancellationToken.None);

            Assert.Contains(store.Definitions, definition => definition.Id == "lofree.flow-lite");
            Assert.Empty(store.Diagnostics);
        });
    }

    [Fact]
    public async Task UserDefinition_OverridesPluginDefinitionById()
    {
        await WithStoreAsync(async (store, paths, catalog) =>
        {
            catalog.DeviceDefinitions.Add(LofreeFixture());
            Directory.CreateDirectory(paths.DefinitionsDirectory);
            var fixture = await File.ReadAllTextAsync(LofreeFixture());
            var overridden = fixture.Replace(
                "\"displayName\": \"Lofree Flow Lite\"",
                "\"displayName\": \"Lofree Override\"",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(
                Path.Combine(paths.DefinitionsDirectory, "lofree.custom.json"),
                overridden);

            await store.ReloadAsync(CancellationToken.None);

            var definition = Assert.Single(
                store.Definitions,
                item => item.Id == "lofree.flow-lite");
            Assert.Equal("Lofree Override", definition.DisplayName);
        });
    }

    [Fact]
    public async Task InvalidUserDefinition_IsReportedAndSkipped()
    {
        await WithStoreAsync(async (store, paths, catalog) =>
        {
            catalog.DeviceDefinitions.Add(LofreeFixture());
            Directory.CreateDirectory(paths.DefinitionsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(paths.DefinitionsDirectory, "broken.json"),
                """{ "schemaVersion": 1, "id": "broken" }""");

            await store.ReloadAsync(CancellationToken.None);

            Assert.Contains(store.Definitions, definition => definition.Id == "lofree.flow-lite");
            Assert.Contains(store.Diagnostics, diagnostic =>
                diagnostic.Source.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase));
        });
    }

    private static string LofreeFixture() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "lofree.flow-lite.json");

    private static async Task WithStoreAsync(
        Func<JsonDeviceDefinitionStore, TestAppPaths, FakePluginAssetCatalog, Task> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TinyBattery.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new TestAppPaths(root);
        var catalog = new FakePluginAssetCatalog();
        var store = new JsonDeviceDefinitionStore(
            paths,
            catalog,
            new DefinitionValidator(),
            NullLogger<JsonDeviceDefinitionStore>.Instance);

        try
        {
            await action(store, paths, catalog);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
