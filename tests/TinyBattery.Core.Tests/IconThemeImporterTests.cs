using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Core.Importing;
using TinyBattery.Core.Themes;

namespace TinyBattery.Core.Tests;

public sealed class IconThemeImporterTests
{
    [Fact]
    public async Task FolderTheme_IsImportedWithAssets()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source");
            WriteTheme(source, "blue");
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                source,
                new ImportOptions(),
                CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            var installed = Assert.Single(result.InstalledPaths);
            Assert.True(File.Exists(Path.Combine(installed, "theme.json")));
            Assert.True(File.Exists(Path.Combine(installed, "icon.svg")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExistingTheme_RequiresOverwrite()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source");
            WriteTheme(source, "blue");
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);
            await importer.ImportAsync(source, new ImportOptions(), CancellationToken.None);

            var conflict = await importer.ImportAsync(
                source,
                new ImportOptions(),
                CancellationToken.None);
            Assert.False(conflict.Success);

            var overwrite = await importer.ImportAsync(
                source,
                new ImportOptions(Overwrite: true),
                CancellationToken.None);
            Assert.True(overwrite.Success, string.Join(Environment.NewLine, overwrite.Diagnostics));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ZipTheme_IsImported()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source");
            WriteTheme(source, "blue");
            var archive = Path.Combine(root, "blue.zip");
            ZipFile.CreateFromDirectory(source, archive);

            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                archive,
                new ImportOptions(),
                CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.True(File.Exists(Path.Combine(result.InstalledPaths[0], "theme.json")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ThemeWithMissingAsset_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(
                Path.Combine(source, "theme.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "broken",
                  "name": "Broken",
                  "kind": "imageSet",
                  "levels": [
                    { "minPercent": 0, "asset": "missing.png" }
                  ]
                }
                """);
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                source,
                new ImportOptions(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Diagnostics);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ClassicTheme_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(
                Path.Combine(source, "theme.json"),
                """
                {
                  "schemaVersion": 1,
                  "id": "legacy",
                  "name": "Legacy",
                  "kind": "classic"
                }
                """);
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                source,
                new ImportOptions(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Contains("Only standard SVG", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static IconThemeImporter CreateImporter(TestAppPaths paths) =>
        new(
            paths,
            new IconThemeLoader(
                new IconThemeValidator(),
                new FakeImageAssetValidator()),
            new SafeZipExtractor(),
            NullLogger<IconThemeImporter>.Instance);

    private static void WriteTheme(string directory, string id)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "theme.json"),
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "{{id}}",
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
        File.WriteAllText(
            Path.Combine(directory, "icon.svg"),
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
              <defs>
                <rect id="tb-fill" x="4" y="4" width="24" height="24" />
              </defs>
            </svg>
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
