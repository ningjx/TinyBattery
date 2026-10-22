using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Core.Importing;
using TinyBattery.Core.Themes;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Tests;

public sealed class PackageImporterTests
{
    [Fact]
    public async Task PackageWithDeviceAndTheme_ImportsBoth()
    {
        var root = CreateRoot();
        try
        {
            var package = Path.Combine(root, "package");
            Directory.CreateDirectory(Path.Combine(package, "devices"));
            Directory.CreateDirectory(Path.Combine(package, "themes", "blue"));

            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "lofree.flow-lite.json"),
                Path.Combine(package, "devices", "lofree.json"));
            await File.WriteAllTextAsync(
                Path.Combine(package, "themes", "blue", "theme.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "blue",
                  "name": "Blue",
                  "kind": "classic"
                }
                """);
            await File.WriteAllTextAsync(
                Path.Combine(package, "package.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "example.pack",
                  "name": "Example Pack",
                  "version": "1.0.0",
                  "devices": ["devices/lofree.json"],
                  "themes": ["themes/blue"]
                }
                """);

            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                package,
                new ImportOptions(),
                CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(2, result.InstalledPaths.Count);
            Assert.True(File.Exists(Path.Combine(paths.DefinitionsDirectory, "lofree.flow-lite.json")));
            Assert.True(File.Exists(Path.Combine(paths.ThemesDirectory, "blue", "theme.json")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PackageJsonFile_IsImported()
    {
        var root = CreateRoot();
        try
        {
            var package = Path.Combine(root, "package");
            Directory.CreateDirectory(Path.Combine(package, "devices"));
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "lofree.flow-lite.json"),
                Path.Combine(package, "devices", "lofree.json"));
            var manifest = Path.Combine(package, "package.json");
            await File.WriteAllTextAsync(
                manifest,
                """
                {
                  "schemaVersion": 1,
                  "id": "example.pack",
                  "name": "Example Pack",
                  "version": "1.0.0",
                  "devices": ["devices/lofree.json"]
                }
                """);

            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                manifest,
                new ImportOptions(),
                CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.True(File.Exists(Path.Combine(paths.DefinitionsDirectory, "lofree.flow-lite.json")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PackageEntryEscapingRoot_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            var package = Path.Combine(root, "package");
            Directory.CreateDirectory(package);
            await File.WriteAllTextAsync(
                Path.Combine(package, "package.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "example.pack",
                  "name": "Example Pack",
                  "version": "1.0.0",
                  "devices": ["../outside.json"]
                }
                """);
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                package,
                new ImportOptions(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Contains("escapes", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static PackageImporter CreateImporter(TestAppPaths paths)
    {
        var devices = new DeviceDefinitionImporter(
            paths,
            new FakeProtocolRegistry("hid.request-response"),
            new DefinitionValidator(),
            NullLogger<DeviceDefinitionImporter>.Instance);
        var themes = new IconThemeImporter(
            paths,
            new IconThemeLoader(
                new IconThemeValidator(),
                new FakeImageAssetValidator()),
            new SafeZipExtractor(),
            NullLogger<IconThemeImporter>.Instance);

        return new PackageImporter(
            devices,
            themes,
            new PackageValidator(),
            new SafeZipExtractor(),
            NullLogger<PackageImporter>.Instance);
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
