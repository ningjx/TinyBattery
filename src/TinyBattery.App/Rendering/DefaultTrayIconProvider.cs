using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace TinyBattery.App.Rendering;

public interface IDefaultTrayIconProvider : IDisposable
{
    Drawing.Icon GetDefaultIcon(int size);
}

public sealed class DefaultTrayIconProvider : IDefaultTrayIconProvider
{
    private readonly Dictionary<int, Drawing.Icon> _icons = [];

    public Drawing.Icon GetDefaultIcon(int size)
    {
        size = Math.Clamp(size, 8, 128);
        if (_icons.TryGetValue(size, out var icon))
        {
            return icon;
        }

        icon = Create(size);
        _icons[size] = icon;
        return icon;
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    private static Drawing.Icon Create(int size)
    {
        var scale = size / 32f;
        var shellThickness = Math.Max(1.5f, 2.2f * scale);

        using var bitmap = new Drawing.Bitmap(
            size,
            size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality;
            graphics.Clear(Drawing.Color.Transparent);

            using var tile = RoundedRect(
                new Drawing.RectangleF(2f * scale, 2f * scale, 28f * scale, 28f * scale),
                7f * scale);
            using var tileBrush = new Drawing.SolidBrush(
                Drawing.Color.FromArgb(0x1F, 0x2A, 0x37));
            graphics.FillPath(tileBrush, tile);

            var bodyRect = new Drawing.RectangleF(
                7f * scale,
                11f * scale,
                17f * scale,
                11f * scale);
            var cavityRect = new Drawing.RectangleF(
                bodyRect.X + shellThickness,
                bodyRect.Y + shellThickness,
                Math.Max(1f, bodyRect.Width - shellThickness * 2),
                Math.Max(1f, bodyRect.Height - shellThickness * 2));

            using var outer = RoundedRect(bodyRect, 3f * scale);
            using var inner = RoundedRect(cavityRect, 1.6f * scale);
            using var shell = new Drawing2D.GraphicsPath(Drawing2D.FillMode.Alternate);
            shell.AddPath(outer, connect: false);
            shell.AddPath(inner, connect: false);
            using var white = new Drawing.SolidBrush(Drawing.Color.White);
            graphics.FillPath(white, shell);

            using var terminal = RoundedRect(
                new Drawing.RectangleF(
                    bodyRect.Right + 0.2f * scale,
                    14.5f * scale,
                    2.6f * scale,
                    4f * scale),
                1.3f * scale);
            graphics.FillPath(white, terminal);

            using var bolt = new Drawing.SolidBrush(
                Drawing.Color.FromArgb(0x45, 0xC9, 0x6B));
            graphics.FillPolygon(bolt,
            [
                new Drawing.PointF(15.8f * scale, 13.8f * scale),
                new Drawing.PointF(12.4f * scale, 17.6f * scale),
                new Drawing.PointF(14.8f * scale, 17.6f * scale),
                new Drawing.PointF(13.2f * scale, 20.2f * scale),
                new Drawing.PointF(17.4f * scale, 16.2f * scale),
                new Drawing.PointF(15.0f * scale, 16.2f * scale),
            ]);
        }

        return IconFactory.FromBitmap(bitmap);
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
