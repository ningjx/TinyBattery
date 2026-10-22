using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace TinyBattery.App.Rendering;

public sealed class ClassicBatteryIconRenderer : IBatteryIconRenderer
{
    private readonly BatteryPalette _palette;

    public ClassicBatteryIconRenderer(
        ClassicIconThemeOptions? options = null,
        string themeId = "classic")
    {
        _palette = new BatteryPalette(options ?? new ClassicIconThemeOptions());
        ThemeId = themeId;
    }

    public string ThemeId { get; }

    public Drawing.Icon Render(BatteryIconState state, int size)
    {
        using var bitmap = RenderBitmap(state, size);
        return IconFactory.FromBitmap(bitmap);
    }

    public Drawing.Bitmap RenderBitmap(BatteryIconState state, int size)
    {
        size = Math.Clamp(size, 8, 128);
        var scale = size / 32f;
        const float minShell = 1.5f;
        var shellThickness = Math.Max(minShell, 2.3f * scale);

        var bitmap = new Drawing.Bitmap(
            size,
            size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality;
            graphics.Clear(Drawing.Color.Transparent);

            var bodyRect = new Drawing.RectangleF(
                1.5f * scale,
                7.5f * scale,
                26.5f * scale,
                17f * scale);
            var cavityRect = new Drawing.RectangleF(
                bodyRect.X + shellThickness,
                bodyRect.Y + shellThickness,
                Math.Max(1f, bodyRect.Width - shellThickness * 2),
                Math.Max(1f, bodyRect.Height - shellThickness * 2));

            using var outer = RoundedRect(bodyRect, 4.5f * scale);
            using var inner = RoundedRect(cavityRect, 2.2f * scale);
            using var shell = new Drawing2D.GraphicsPath(Drawing2D.FillMode.Alternate);
            shell.AddPath(outer, connect: false);
            shell.AddPath(inner, connect: false);
            using var shellBrush = new Drawing.SolidBrush(BatteryPalette.Shell);
            graphics.FillPath(shellBrush, shell);

            var terminalRect = new Drawing.RectangleF(
                bodyRect.Right - 0.5f * scale,
                bodyRect.Y + 5f * scale,
                3.5f * scale,
                7f * scale);
            using var terminal = RoundedRect(terminalRect, 1.75f * scale);
            graphics.FillPath(shellBrush, terminal);

            var normalized = _palette.GetRatio(state.Percent);
            if (normalized > 0f)
            {
                var fillRect = new Drawing.RectangleF(
                    cavityRect.X,
                    cavityRect.Y,
                    cavityRect.Width * normalized,
                    cavityRect.Height);
                using var fill = RoundedRect(fillRect, 2.2f * scale);
                using var fillBrush = new Drawing.SolidBrush(
                    _palette.GetColor(state.Percent));
                graphics.FillPath(fillBrush, fill);
            }
        }

        return bitmap;
    }

    public void Dispose()
    {
    }

    private static Drawing2D.GraphicsPath RoundedRect(
        Drawing.RectangleF rectangle,
        float radius)
    {
        radius = Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2f);
        var diameter = radius * 2;
        var path = new Drawing2D.GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(
            rectangle.Right - diameter,
            rectangle.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
