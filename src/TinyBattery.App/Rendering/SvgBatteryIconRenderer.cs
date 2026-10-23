using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
#pragma warning disable CS0618
using SkiaSharp;
using Svg.Skia;
using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public sealed class SvgBatteryIconRenderer : IBatteryIconRenderer
{
    private readonly SvgThemeOptions _options;
    private readonly SKSvg _baseSvg;
    private readonly SKRect _bounds;
    private readonly Dictionary<string, SlotGeometry> _geometry = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SKSvg> _stateSvgs = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly SKSvg? _chargingSvg;

    public SvgBatteryIconRenderer(IconThemeDescriptor theme)
    {
        ThemeId = theme.Id;
        _options = theme.Svg
            ?? throw new InvalidOperationException(
                $"Theme '{theme.Id}' has no SVG render options.");

        _baseSvg = LoadSvg(_options.SvgPath);
        _bounds = ReadBounds(_options.SvgPath, _baseSvg.Picture!.CullRect);
        foreach (var slot in _options.Slots)
        {
            if (slot.Mode == IconFillMode.Tint)
            {
                continue;
            }

            _geometry[slot.Id] = ReadGeometry(_options.SvgPath, slot);
        }

        foreach (var (state, path) in _options.StateOverrides ?? new Dictionary<string, string>())
        {
            if (File.Exists(path))
            {
                _stateSvgs[state] = LoadSvg(path);
            }
        }

        if (File.Exists(_options.ChargingOverlayPath))
        {
            _chargingSvg = LoadSvg(_options.ChargingOverlayPath!);
        }
    }

    public string ThemeId { get; }

    public Drawing.Icon Render(BatteryIconFrame frame, int size)
    {
        using var bitmap = RenderBitmap(frame, size);
        return IconFactory.FromBitmap(bitmap);
    }

    public Drawing.Bitmap RenderBitmap(BatteryIconFrame frame, int size)
    {
        size = Math.Clamp(size, 8, 128);
        var info = new SKImageInfo(
            size,
            size,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = _bounds;
        var scale = size / Math.Max(bounds.Width, bounds.Height);
        var offsetX = (size - bounds.Width * scale) / 2f - bounds.Left * scale;
        var offsetY = (size - bounds.Height * scale) / 2f - bounds.Top * scale;

        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);

        DrawBase(canvas, frame, bounds, out var drawFills);
        if (drawFills)
        {
            DrawFills(canvas, frame);
        }

        _chargingSvg?.Draw(canvas);

        canvas.Restore();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var source = new Drawing.Bitmap(stream);
        return new Drawing.Bitmap(source);
    }

    public void Dispose()
    {
        _baseSvg.Dispose();
        foreach (var svg in _stateSvgs.Values)
        {
            svg.Dispose();
        }

        _stateSvgs.Clear();
        _chargingSvg?.Dispose();
    }

    private void DrawBase(
        SKCanvas canvas,
        BatteryIconFrame frame,
        SKRect bounds,
        out bool drawFills)
    {
        drawFills = true;
        var slot = frame.Slots.FirstOrDefault();
        var stateName = slot.IsAvailable
            ? StateName(slot.Percent)
            : "unknown";
        if (_stateSvgs.TryGetValue(stateName, out var overrideSvg))
        {
            overrideSvg.Draw(canvas);
            drawFills = false;
            return;
        }

        if (_options.Slots.Count == 1
            && _options.Slots[0].Mode == IconFillMode.Tint)
        {
            DrawTintedBase(canvas, bounds, ColorFor(slot));
            drawFills = false;
            return;
        }

        _baseSvg.Draw(canvas);
    }

    private void DrawTintedBase(SKCanvas canvas, SKRect bounds, SKColor color)
    {
        canvas.SaveLayer(new SKPaint());
        _baseSvg.Draw(canvas);
        using var paint = new SKPaint
        {
            Color = color,
            BlendMode = SKBlendMode.SrcIn,
        };
        canvas.DrawRect(bounds, paint);
        canvas.Restore();
    }

    private void DrawFills(SKCanvas canvas, BatteryIconFrame frame)
    {
        for (var index = 0; index < _options.Slots.Count; index++)
        {
            var slot = _options.Slots[index];
            if (slot.Mode == IconFillMode.Tint)
            {
                continue;
            }

            var state = index < frame.Slots.Count
                ? frame.Slots[index]
                : new BatteryIconSlotState(slot.Id, null, false, false);
            if (!_geometry.TryGetValue(slot.Id, out var geometry))
            {
                continue;
            }

            var fraction = state.HasReading
                ? Math.Clamp(state.Percent!.Value / 100f, 0f, 1f)
                : 0f;
            if (fraction <= 0f)
            {
                continue;
            }

            var color = ColorFor(state);
            switch (slot.Mode)
            {
                case IconFillMode.Rect:
                    DrawRectFill(canvas, geometry, fraction, color);
                    break;
                case IconFillMode.Ring:
                    DrawRing(canvas, geometry, fraction, color);
                    break;
                case IconFillMode.Sector:
                    DrawSector(canvas, geometry, fraction, color);
                    break;
            }
        }
    }

    private static void DrawRectFill(
        SKCanvas canvas,
        SlotGeometry geometry,
        float fraction,
        SKColor color)
    {
        var rect = geometry.Rect;
        var height = rect.Height * fraction;
        var fill = new SKRect(
            rect.Left,
            rect.Bottom - height,
            rect.Right,
            rect.Bottom);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
        };
        canvas.DrawRect(fill, paint);
    }

    private void DrawRing(
        SKCanvas canvas,
        SlotGeometry geometry,
        float fraction,
        SKColor color)
    {
        var oval = new SKRect(
            geometry.Center.X - geometry.Radius,
            geometry.Center.Y - geometry.Radius,
            geometry.Center.X + geometry.Radius,
            geometry.Center.Y + geometry.Radius);
        var width = Math.Max(0.5f, geometry.StrokeWidth);

        if (_options.Colors.Track is { } track)
        {
            using var trackPaint = new SKPaint
            {
                Color = ParseColor(track, SKColors.Transparent),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = width,
            };
            canvas.DrawCircle(geometry.Center, geometry.Radius, trackPaint);
        }

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            StrokeCap = SKStrokeCap.Round,
        };
        if (fraction >= 0.999f)
        {
            canvas.DrawCircle(geometry.Center, geometry.Radius, paint);
            return;
        }

        var direction = _options.Clockwise ? 1f : -1f;
        using var path = new SKPath();
        path.AddArc(oval, (float)_options.StartAngle, 360f * fraction * direction);
        canvas.DrawPath(path, paint);
    }

    private void DrawSector(
        SKCanvas canvas,
        SlotGeometry geometry,
        float fraction,
        SKColor color)
    {
        var sweep = 360f * fraction * (_options.Clockwise ? 1f : -1f);
        if (Math.Abs(sweep) >= 359.9f)
        {
            using var fullPaint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawCircle(geometry.Center, geometry.Radius, fullPaint);
            return;
        }

        var center = geometry.Center;
        var radius = geometry.Radius;
        var inner = Math.Clamp((float)_options.InnerRadius, 0f, radius);
        var start = (float)_options.StartAngle;
        var end = start + sweep;

        using var path = new SKPath();
        var startPoint = PointOnCircle(center, radius, start);
        var endPoint = PointOnCircle(center, radius, end);
        path.MoveTo(center);
        path.LineTo(startPoint);
        path.ArcTo(
            new SKRect(
                center.X - radius,
                center.Y - radius,
                center.X + radius,
                center.Y + radius),
            start,
            sweep,
            forceMoveTo: false);

        if (inner > 0f)
        {
            var innerEnd = PointOnCircle(center, inner, end);
            var innerStart = PointOnCircle(center, inner, start);
            path.LineTo(innerEnd);
            path.ArcTo(
                new SKRect(
                    center.X - inner,
                    center.Y - inner,
                    center.X + inner,
                    center.Y + inner),
                end,
                -sweep,
                forceMoveTo: false);
            path.LineTo(innerStart);
        }
        else
        {
            path.LineTo(center);
        }

        path.Close();
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    private static SKPoint PointOnCircle(SKPoint center, float radius, float angle)
    {
        var radians = angle * MathF.PI / 180f;
        return new SKPoint(
            center.X + radius * MathF.Cos(radians),
            center.Y + radius * MathF.Sin(radians));
    }

    private SKColor ColorFor(BatteryIconSlotState slot)
    {
        if (!slot.HasReading)
        {
            return ParseColor(_options.Colors.Unknown, SKColors.Gray);
        }

        var percent = slot.Percent!.Value;
        return percent switch
        {
            >= 60 => ParseColor(_options.Colors.Healthy, SKColors.Green),
            >= 20 => ParseColor(_options.Colors.Warning, SKColors.Orange),
            _ => ParseColor(_options.Colors.Critical, SKColors.Red),
        };
    }

    private static string StateName(int? percent) => percent switch
    {
        null => "unknown",
        >= 90 => "full",
        >= 60 => "high",
        >= 20 => "mid",
        _ => "low",
    };

    private static SKSvg LoadSvg(string path)
    {
        var svg = SKSvg.CreateFromFile(path);
        if (svg.Picture is null)
        {
            svg.Dispose();
            throw new InvalidDataException($"SVG '{path}' could not be parsed.");
        }

        return svg;
    }

    private static SKRect ReadBounds(string svgPath, SKRect fallback)
    {
        try
        {
            using var stream = File.OpenRead(svgPath);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var document = XDocument.Load(reader);
            var root = document.Root;
            var viewBox = (string?)root?.Attribute("viewBox");
            if (!string.IsNullOrWhiteSpace(viewBox))
            {
                var parts = viewBox
                    .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => float.TryParse(
                        part,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value)
                            ? value
                            : float.NaN)
                    .ToArray();
                if (parts.Length == 4
                    && parts.All(value => float.IsFinite(value))
                    && parts[2] > 0
                    && parts[3] > 0)
                {
                    return new SKRect(parts[0], parts[1], parts[0] + parts[2], parts[1] + parts[3]);
                }
            }

            var width = ParseLength((string?)root?.Attribute("width"));
            var height = ParseLength((string?)root?.Attribute("height"));
            if (width is > 0 && height is > 0)
            {
                return new SKRect(0, 0, width.Value, height.Value);
            }
        }
        catch (Exception)
        {
        }

        return fallback.Width > 0 && fallback.Height > 0
            ? fallback
            : new SKRect(0, 0, 32, 32);
    }

    private static float? ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().Replace("px", string.Empty, StringComparison.OrdinalIgnoreCase);
        return float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }

    private static SKColor ParseColor(string value, SKColor fallback)
    {
        try
        {
            return SKColor.Parse(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static SlotGeometry ReadGeometry(string svgPath, IconThemeSlot slot)
    {
        using var stream = File.OpenRead(svgPath);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var document = XDocument.Load(reader);
        var element = document
            .Descendants()
            .FirstOrDefault(candidate => string.Equals(
                (string?)candidate.Attribute("id"),
                slot.Target,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"SVG target '{slot.Target}' was not found in '{svgPath}'.");

        if (!string.IsNullOrWhiteSpace((string?)element.Attribute("transform")))
        {
            throw new InvalidDataException(
                $"SVG target '{slot.Target}' must not have a transform.");
        }

        var name = element.Name.LocalName;
        if (slot.Mode == IconFillMode.Ring || slot.Mode == IconFillMode.Sector)
        {
            if (!string.Equals(name, "circle", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SVG target '{slot.Target}' must be a <circle> for {slot.Mode}.");
            }

            return new SlotGeometry
            {
                Center = new SKPoint(
                    ReadFloat(element, "cx"),
                    ReadFloat(element, "cy")),
                Radius = ReadFloat(element, "r"),
                StrokeWidth = ReadStrokeWidth(element),
            };
        }

        if (!string.Equals(name, "rect", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SVG target '{slot.Target}' must be a <rect> for {slot.Mode}.");
        }

        var x = ReadFloat(element, "x");
        var y = ReadFloat(element, "y");
        return new SlotGeometry
        {
            Rect = new SKRect(
                x,
                y,
                x + ReadFloat(element, "width"),
                y + ReadFloat(element, "height")),
        };
    }

    private static float ReadFloat(XElement element, string name) =>
        float.TryParse(
            (string?)element.Attribute(name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0f;

    private static float ReadStrokeWidth(XElement element)
    {
        var attribute = (string?)element.Attribute("stroke-width");
        if (float.TryParse(
                attribute,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var width))
        {
            return width;
        }

        var style = (string?)element.Attribute("style");
        if (!string.IsNullOrWhiteSpace(style))
        {
            foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = declaration.Split(':', 2);
                if (parts.Length == 2
                    && string.Equals(parts[0].Trim(), "stroke-width", StringComparison.OrdinalIgnoreCase)
                    && float.TryParse(
                        parts[1].Trim().Replace("px", ""),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var styledWidth))
                {
                    return styledWidth;
                }
            }
        }

        return 1f;
    }

    private sealed class SlotGeometry
    {
        public SKRect Rect { get; init; }

        public SKPoint Center { get; init; }

        public float Radius { get; init; }

        public float StrokeWidth { get; init; }
    }
}
