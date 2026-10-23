using System.IO;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Themes;

namespace TinyBattery.Core.Tests;

public sealed class SvgThemeLoaderTests
{
    [Fact]
    public void LoadsFixedSlotSvgTheme()
    {
        var root = CreateRoot();
        try
        {
            var svg = """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
                  <defs>
                    <circle id="ring-a" cx="8" cy="16" r="5" fill="none" stroke-width="2" />
                    <circle id="ring-b" cx="24" cy="16" r="5" fill="none" stroke-width="2" />
                  </defs>
                </svg>
                """;
            File.WriteAllText(Path.Combine(root, "icon.svg"), svg);
            File.WriteAllText(Path.Combine(root, "theme.json"), """
                {
                  "schemaVersion": 1,
                  "id": "test.svg",
                  "name": "Test SVG",
                  "version": "1.0.0",
                  "kind": "svg",
                  "slotCount": 2,
                  "render": {
                    "svg": "icon.svg",
                    "slots": [
                      { "id": "left", "label": "左", "mode": "ring", "target": "ring-a" },
                      { "id": "right", "label": "右", "mode": "ring", "target": "ring-b" }
                    ]
                  }
                }
                """);

            var loader = new IconThemeLoader(
                new IconThemeValidator(),
                new FakeImageAssetValidator());

            var ok = loader.TryLoad(
                Path.Combine(root, "theme.json"),
                out var theme,
                out var errors);

            Assert.True(ok, string.Join(" | ", errors));
            Assert.NotNull(theme);
            Assert.Equal(IconThemeKind.Svg, theme.Kind);
            Assert.Equal(2, theme.SlotCount);
            Assert.Equal("left", theme.Slots[0].Id);
            Assert.Equal(IconFillMode.Ring, theme.Slots[0].Mode);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RejectsMissingSlotTarget()
    {
        var root = CreateRoot();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "icon.svg"),
                """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" />""");
            File.WriteAllText(Path.Combine(root, "theme.json"), """
                {
                  "schemaVersion": 1,
                  "id": "test.svg",
                  "name": "Test SVG",
                  "kind": "svg",
                  "slotCount": 1,
                  "render": {
                    "svg": "icon.svg",
                    "slots": [
                      { "id": "main", "mode": "rect", "target": "missing" }
                    ]
                  }
                }
                """);

            var loader = new IconThemeLoader(
                new IconThemeValidator(),
                new FakeImageAssetValidator());

            Assert.False(loader.TryLoad(
                Path.Combine(root, "theme.json"),
                out _,
                out var errors));
            Assert.Contains(errors, error => error.Contains(
                "was not found",
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TinyBattery.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
