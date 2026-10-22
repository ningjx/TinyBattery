using TinyBattery.App.Rendering;
using Drawing = System.Drawing;

namespace TinyBattery.App.Tests;

public sealed class BatteryPaletteTests
{
    [Theory]
    [InlineData(60, 0x45, 0xC9, 0x6B)]
    [InlineData(100, 0x45, 0xC9, 0x6B)]
    [InlineData(20, 0xD9, 0x77, 0x06)]
    [InlineData(59, 0xD9, 0x77, 0x06)]
    [InlineData(0, 0xC4, 0x2B, 0x1C)]
    [InlineData(19, 0xC4, 0x2B, 0x1C)]
    public void GetFillColor_MatchesOriginalThresholds(
        int percent,
        int red,
        int green,
        int blue)
    {
        var color = BatteryPalette.GetFillColor(percent);

        Assert.Equal(Drawing.Color.FromArgb(red, green, blue), color);
    }

    [Fact]
    public void UnknownPercent_UsesGrayAndThirtyFivePercentFill()
    {
        Assert.Equal(BatteryPalette.UnknownFill, BatteryPalette.GetFillColor(null));
        Assert.Equal(0.35f, BatteryPalette.GetFillRatio(null));
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(50, 0.5f)]
    [InlineData(100, 1f)]
    [InlineData(150, 1f)]
    [InlineData(-10, 0f)]
    public void GetFillRatio_ClampsToValidRange(int percent, float expected)
    {
        Assert.Equal(expected, BatteryPalette.GetFillRatio(percent));
    }
}
