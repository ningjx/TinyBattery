using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Rendering;

namespace TinyBattery.App.Tests;

public sealed class ClassicBatteryIconRendererTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Render_Produces32By32Icon(int? percent)
    {
        var renderer = new ClassicBatteryIconRenderer();
        using var icon = renderer.Render(new BatteryIconState(percent), 32);

        Assert.Equal(32, icon.Width);
        Assert.Equal(32, icon.Height);
    }

    [Fact]
    public void RenderBitmap_Produces32By32Bitmap()
    {
        var renderer = new ClassicBatteryIconRenderer();
        using var bitmap = renderer.RenderBitmap(new BatteryIconState(75), 32);

        Assert.Equal(32, bitmap.Width);
        Assert.Equal(32, bitmap.Height);
    }

    [Fact]
    public void RenderBitmap_At16_Produces16By16Bitmap()
    {
        var renderer = new ClassicBatteryIconRenderer();
        using var bitmap = renderer.RenderBitmap(new BatteryIconState(80), 16);

        Assert.Equal(16, bitmap.Width);
        Assert.Equal(16, bitmap.Height);
    }

    [Fact]
    public void Cache_ReturnsSameInstanceForSameState()
    {
        using var cache = new BatteryIconCache(new ClassicBatteryIconRenderer());
        var state = new BatteryIconState(50);

        var first = cache.Get(state, 32);
        var second = cache.Get(state, 32);

        Assert.Same(first, second);
    }

    [Fact]
    public void CustomClassicTheme_UsesConfiguredColor()
    {
        var renderer = new ClassicBatteryIconRenderer(
            new TinyBattery.Abstractions.Themes.ClassicIconThemeOptions(
                HealthyColor: "#2F80ED"));
        using var bitmap = renderer.RenderBitmap(new BatteryIconState(95), 32);

        Assert.Equal(
            System.Drawing.Color.FromArgb(0x2F, 0x80, 0xED).ToArgb(),
            bitmap.GetPixel(10, 16).ToArgb());
    }
}
