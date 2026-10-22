using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Core.Importing;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Tests;

public sealed class DeviceDefinitionImporterTests
{
    [Fact]
    public async Task ValidDefinition_IsImported()
    {
        var root = CreateRoot();
        try
        {
            var paths = new TestAppPaths(root);
            var importer = CreateImporter(paths);

            var result = await importer.ImportAsync(
                LofreeFixture(),
                new ImportOptions(),
                CancellationToken.None);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            var installed = Assert.Single(result.InstalledPaths);
            Assert.True(File.Exists(installed));
            Assert.EndsWith("lofree.flow-lite.json", installed, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ExistingDefinition_RequiresOverwrite()
    {
        var root = CreateRoot();
        try
        {
            var paths = new TestAppPaths(root);
            var importer = CreateImporter(paths);
            await importer.ImportAsync(LofreeFixture(), new ImportOptions(), CancellationToken.None);

            var conflict = await importer.ImportAsync(
                LofreeFixture(),
                new ImportOptions(),
                CancellationToken.None);
            Assert.False(conflict.Success);

            var overwrite = await importer.ImportAsync(
                LofreeFixture(),
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
    public async Task InvalidDefinition_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "broken.json");
            await File.WriteAllTextAsync(source, """{ "schemaVersion": 1 }""");
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
    public async Task UnknownProtocol_IsRejected()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "unknown.json");
            await File.WriteAllTextAsync(
                source,
                """
                {
                  "schemaVersion": 1,
                  "id": "example.unknown",
                  "displayName": "Unknown",
                  "match": { "transport": "hid", "vendorId": 1 },
                  "protocol": { "kind": "missing.protocol", "options": {} },
                  "polling": { "intervalSeconds": 60 }
                }
                """);
            var paths = new TestAppPaths(Path.Combine(root, "data"));
            var importer = new DeviceDefinitionImporter(
                paths,
                new FakeProtocolRegistry("hid.request-response"),
                new DefinitionValidator(),
                NullLogger<DeviceDefinitionImporter>.Instance);

            var result = await importer.ImportAsync(
                source,
                new ImportOptions(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Contains("not registered", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static DeviceDefinitionImporter CreateImporter(TestAppPaths paths) =>
        new(
            paths,
            new FakeProtocolRegistry("hid.request-response"),
            new DefinitionValidator(),
            NullLogger<DeviceDefinitionImporter>.Instance);

    private static string LofreeFixture() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "lofree.flow-lite.json");

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
