using System.IO;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace TinyBattery.App.Tests;

public sealed class ImageSetBatteryIconRendererTests
{
    [Theory]
    [InlineData(95, "full")]
    [InlineData(70, "high")]
    [InlineData(50, "mid")]
    [InlineData(5, "low")]
    [InlineData(null, "unknown")]
    public void SelectsAssetForBatteryLevel(int? percent, string expectedName)
    {
        var root = CreateRoot();
        try
        {
            var colors = new Dictionary<string, Drawing.Color>
            {
                ["full"] = Drawing.Color.FromArgb(0x11, 0x11, 0x11),
                ["high"] = Drawing.Color.FromArgb(0x22, 0x22, 0x22),
                ["mid"] = Drawing.Color.FromArgb(0x33, 0x33, 0x33),
                ["low"] = Drawing.Color.FromArgb(0x44, 0x44, 0x44),
                ["unknown"] = Drawing.Color.FromArgb(0x55, 0x55, 0x55),
            };
            foreach (var (name, color) in colors)
            {
                CreatePng(Path.Combine(root, name + ".png"), color);
            }

            var theme = new IconThemeDescriptor(
                "images",
                "Images",
                "1.0",
                "test",
                "",
                IconThemeKind.ImageSet,
                SourceDirectory: root,
                Levels:
                [
                    new IconThemeLevel(0, Path.Combine(root, "low.png")),
                    new IconThemeLevel(20, Path.Combine(root, "mid.png")),
                    new IconThemeLevel(60, Path.Combine(root, "high.png")),
                    new IconThemeLevel(90, Path.Combine(root, "full.png")),
                ],
                UnknownAssetPath: Path.Combine(root, "unknown.png"));

            using var renderer = new ImageSetBatteryIconRenderer(theme);
            using var bitmap = renderer.RenderBitmap(new BatteryIconState(percent), 32);

            Assert.Equal(colors[expectedName].ToArgb(), bitmap.GetPixel(16, 16).ToArgb());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Render_Produces32By32Icon()
    {
        var root = CreateRoot();
        try
        {
            CreatePng(Path.Combine(root, "low.png"), Drawing.Color.Green);
            var theme = new IconThemeDescriptor(
                "images",
                "Images",
                null,
                null,
                null,
                IconThemeKind.ImageSet,
                Levels: [new IconThemeLevel(0, Path.Combine(root, "low.png"))]);

            using var renderer = new ImageSetBatteryIconRenderer(theme);
            using var icon = renderer.Render(new BatteryIconState(50), 32);

            Assert.Equal(32, icon.Width);
            Assert.Equal(32, icon.Height);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void SmallAsset_IsUsedAtSmallSizes()
    {
        var root = CreateRoot();
        try
        {
            var large = Path.Combine(root, "large.png");
            var small = Path.Combine(root, "small.png");
            CreatePng(large, Drawing.Color.Red);
            CreatePng(small, Drawing.Color.Blue);

            var theme = new IconThemeDescriptor(
                "images",
                "Images",
                null,
                null,
                null,
                IconThemeKind.ImageSet,
                Levels:
                [
                    new IconThemeLevel(0, large, small),
                ]);

            using var renderer = new ImageSetBatteryIconRenderer(theme);
            using var smallBitmap = renderer.RenderBitmap(new BatteryIconState(80), 16);
            using var largeBitmap = renderer.RenderBitmap(new BatteryIconState(80), 32);

            Assert.Equal(Drawing.Color.Blue.ToArgb(), smallBitmap.GetPixel(8, 8).ToArgb());
            Assert.Equal(Drawing.Color.Red.ToArgb(), largeBitmap.GetPixel(16, 16).ToArgb());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static void CreatePng(string path, Drawing.Color color)
    {
        using var bitmap = new Drawing.Bitmap(
            32,
            32,
            DrawingImaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.Clear(color);
        }

        bitmap.Save(path, DrawingImaging.ImageFormat.Png);
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
