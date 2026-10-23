using System.IO;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;

namespace TinyBattery.App.Tests;

public sealed class SvgBatteryIconRendererTests
{
    [Fact]
    public void RectFill_CoversTargetAtFullBattery()
    {
        var root = CreateRoot();
        try
        {
            var path = WriteSvg(root, """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
                  <defs>
                    <rect id="tb-fill" x="4" y="4" width="24" height="24" />
                  </defs>
                </svg>
                """);
            using var renderer = CreateRenderer(
                path,
                new IconThemeSlot("main", null, IconFillMode.Rect, "tb-fill"));

            using var bitmap = renderer.RenderBitmap(
                Frame(100),
                32);

            var pixel = bitmap.GetPixel(16, 16);
            Assert.True(pixel.G > 200, $"Expected a green fill, got {pixel}.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RectFill_IsEmptyAtZeroBattery()
    {
        var root = CreateRoot();
        try
        {
            var path = WriteSvg(root, """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
                  <defs>
                    <rect id="tb-fill" x="4" y="4" width="24" height="24" />
                  </defs>
                </svg>
                """);
            using var renderer = CreateRenderer(
                path,
                new IconThemeSlot("main", null, IconFillMode.Rect, "tb-fill"));

            using var bitmap = renderer.RenderBitmap(Frame(0), 32);

            Assert.Equal(0, bitmap.GetPixel(16, 16).A);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void RingFill_FillsClockwiseFromTop()
    {
        var root = CreateRoot();
        try
        {
            var path = WriteSvg(root, """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
                  <defs>
                    <circle id="tb-ring" cx="16" cy="16" r="10" fill="none" stroke-width="4" />
                  </defs>
                </svg>
                """);
            using var renderer = CreateRenderer(
                path,
                new IconThemeSlot("main", null, IconFillMode.Ring, "tb-ring"));

            using var bitmap = renderer.RenderBitmap(Frame(50), 32);

            Assert.True(bitmap.GetPixel(26, 16).G > 200);
            Assert.Equal(0, bitmap.GetPixel(6, 16).A);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void TwoSlots_RenderIndependently()
    {
        var root = CreateRoot();
        try
        {
            var path = WriteSvg(root, """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
                  <defs>
                    <rect id="tb-left" x="2" y="8" width="12" height="16" />
                    <rect id="tb-right" x="18" y="8" width="12" height="16" />
                  </defs>
                </svg>
                """);
            var theme = new IconThemeDescriptor(
                "two",
                "Two",
                null,
                null,
                null,
                IconThemeKind.Svg,
                Svg: new SvgThemeOptions(
                    path,
                    [
                        new IconThemeSlot("left", null, IconFillMode.Rect, "tb-left"),
                        new IconThemeSlot("right", null, IconFillMode.Rect, "tb-right"),
                    ],
                    new ThemeColorSet(Healthy: "#00FF00", Warning: "#00FF00")));
            using var renderer = new SvgBatteryIconRenderer(theme);

            using var bitmap = renderer.RenderBitmap(
                new BatteryIconFrame(
                [
                    new BatteryIconSlotState("left", 100, false, true),
                    new BatteryIconSlotState("right", 0, false, true),
                ]),
                32);

            Assert.True(bitmap.GetPixel(8, 16).G > 200);
            Assert.Equal(0, bitmap.GetPixel(24, 16).A);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static SvgBatteryIconRenderer CreateRenderer(
        string svgPath,
        IconThemeSlot slot)
    {
        var theme = new IconThemeDescriptor(
            "test",
            "Test",
            null,
            null,
            null,
            IconThemeKind.Svg,
            Svg: new SvgThemeOptions(
                svgPath,
                [slot],
                new ThemeColorSet(Healthy: "#00FF00", Warning: "#00FF00")));
        return new SvgBatteryIconRenderer(theme);
    }

    private static BatteryIconFrame Frame(int percent) =>
        new([new BatteryIconSlotState("main", percent, false, true)]);

    private static string WriteSvg(string root, string content)
    {
        var path = Path.Combine(root, "icon.svg");
        File.WriteAllText(path, content);
        return path;
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
