using System.IO;
using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace TinyBattery.App.Rendering;

public sealed class PluginBatteryIconRenderer : IBatteryIconRenderer
{
    private readonly IconThemeDescriptor _theme;
    private readonly IBatteryIconThemePlugin _plugin;

    public PluginBatteryIconRenderer(
        IconThemeDescriptor theme,
        IBatteryIconThemePlugin plugin)
    {
        _theme = theme;
        _plugin = plugin;
        ThemeId = theme.Id;
    }

    public string ThemeId { get; }

    public Drawing.Icon Render(BatteryIconFrame frame, int size) =>
        Render(FirstState(frame), size);

    public Drawing.Bitmap RenderBitmap(BatteryIconFrame frame, int size) =>
        RenderBitmap(FirstState(frame), size);

    public Drawing.Icon Render(BatteryIconState state, int size)
    {
        using var bitmap = RenderBitmap(state, size);
        return IconFactory.FromBitmap(bitmap);
    }

    public Drawing.Bitmap RenderBitmap(BatteryIconState state, int size)
    {
        size = Math.Clamp(size, 8, 128);
        var asset = _plugin.Render(state, size);
        using var stream = new MemoryStream(asset.Data, writable: false);
        using var source = Drawing.Image.FromStream(stream);

        var target = new Drawing.Bitmap(
            size,
            size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(target))
        {
            graphics.Clear(Drawing.Color.Transparent);
            graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality;
            graphics.DrawImage(source, 0, 0, size, size);
        }

        return target;
    }

    public void Dispose()
    {
    }

    private static BatteryIconState FirstState(BatteryIconFrame frame)
    {
        var slot = frame.Slots.FirstOrDefault();
        return new BatteryIconState(slot.Percent, slot.IsCharging);
    }
}
