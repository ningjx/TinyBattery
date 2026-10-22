using System.IO;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Themes;

namespace TinyBattery.Core.Tests;

public sealed class IconThemeLoaderTests
{
    [Fact]
    public void ClassicTheme_LoadsCustomColors()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "blue",
              "name": "Blue",
              "kind": "classic",
              "classic": {
                "healthyColor": "#2F80ED",
                "warningColor": "#F2994A",
                "criticalColor": "#EB5757",
                "unknownColor": "#828282"
              }
            }
            """,
            (loader, manifest) =>
            {
                Assert.True(loader.TryLoad(manifest, out var theme, out var errors),
                    string.Join(Environment.NewLine, errors));
                Assert.NotNull(theme);
                Assert.Equal(IconThemeKind.Classic, theme!.Kind);
                Assert.Equal("#2F80ED", theme.Classic!.HealthyColor);
            });
    }

    [Fact]
    public void ImageSetTheme_LoadsAssets()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "images",
              "name": "Images",
              "kind": "imageSet",
              "levels": [
                { "minPercent": 0, "asset": "low.png" },
                { "minPercent": 60, "asset": "high.png" }
              ],
              "unknown": "unknown.png"
            }
            """,
            (loader, manifest) =>
            {
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(manifest)!, "low.png"), [0]);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(manifest)!, "high.png"), [0]);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(manifest)!, "unknown.png"), [0]);

                Assert.True(loader.TryLoad(manifest, out var theme, out var errors),
                    string.Join(Environment.NewLine, errors));
                Assert.NotNull(theme);
                Assert.Equal(2, theme!.Levels!.Count);
                Assert.Equal(60, theme.Levels[0].MinPercent);
            });
    }

    [Fact]
    public void ImageSetTheme_MissingAsset_Fails()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "images",
              "name": "Images",
              "kind": "imageSet",
              "levels": [
                { "minPercent": 0, "asset": "missing.png" }
              ]
            }
            """,
            (loader, manifest) =>
            {
                Assert.False(loader.TryLoad(manifest, out _, out var errors));
                Assert.Contains(errors, error => error.Contains("not found", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public void ImageSetTheme_PathTraversal_Fails()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "images",
              "name": "Images",
              "kind": "imageSet",
              "levels": [
                { "minPercent": 0, "asset": "../evil.png" }
              ]
            }
            """,
            (loader, manifest) =>
            {
                Assert.False(loader.TryLoad(manifest, out _, out var errors));
                Assert.Contains(errors, error => error.Contains("escapes", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public void ImageSetTheme_LoadsSmallAsset()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "images",
              "name": "Images",
              "kind": "imageSet",
              "levels": [
                {
                  "minPercent": 0,
                  "asset": "low.png",
                  "smallAsset": "low-16.png"
                }
              ]
            }
            """,
            (loader, manifest) =>
            {
                var directory = Path.GetDirectoryName(manifest)!;
                File.WriteAllBytes(Path.Combine(directory, "low.png"), [0]);
                File.WriteAllBytes(Path.Combine(directory, "low-16.png"), [0]);

                Assert.True(loader.TryLoad(manifest, out var theme, out var errors),
                    string.Join(Environment.NewLine, errors));
                Assert.NotNull(theme);
                Assert.NotNull(theme!.Levels![0].SmallAssetPath);
            });
    }

    [Fact]
    public void ImageSetTheme_InvalidImage_Fails()
    {
        WithTheme(
            """
            {
              "schemaVersion": 1,
              "id": "images",
              "name": "Images",
              "kind": "imageSet",
              "levels": [
                { "minPercent": 0, "asset": "low.png" }
              ]
            }
            """,
            (loader, manifest) =>
            {
                File.WriteAllBytes(
                    Path.Combine(Path.GetDirectoryName(manifest)!, "low.png"),
                    [0]);
                var invalidLoader = new IconThemeLoader(
                    new IconThemeValidator(),
                    new FakeImageAssetValidator { IsValid = false });

                Assert.False(invalidLoader.TryLoad(manifest, out var theme, out var errors));
                Assert.Null(theme);
                Assert.Contains(errors, error =>
                    error.Contains("invalid image", StringComparison.OrdinalIgnoreCase));
            });
    }

    private static void WithTheme(
        string manifestJson,
        Action<IconThemeLoader, string> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TinyBattery.Tests",
            Guid.NewGuid().ToString("N"));
        var themeDirectory = Path.Combine(root, "theme");
        Directory.CreateDirectory(themeDirectory);
        var manifest = Path.Combine(themeDirectory, "theme.json");
        File.WriteAllText(manifest, manifestJson);

        try
        {
            action(
                new IconThemeLoader(
                    new IconThemeValidator(),
                    new FakeImageAssetValidator()),
                manifest);
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
