using System.IO;
using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace TinyBattery.App.Rendering;

public sealed class ImageSetBatteryIconRenderer : IBatteryIconRenderer
{
    private readonly IconThemeDescriptor _theme;
    private readonly IReadOnlyList<IconThemeLevel> _levels;
    private readonly string? _lowestAsset;
    private readonly string? _lowestSmallAsset;
    private readonly Dictionary<string, Drawing.Bitmap> _sources =
        new(StringComparer.OrdinalIgnoreCase);

    public ImageSetBatteryIconRenderer(IconThemeDescriptor theme)
    {
        _theme = theme;
        _levels = (theme.Levels ?? [])
            .OrderByDescending(level => level.MinPercent)
            .ToArray();
        _lowestAsset = _levels.LastOrDefault()?.AssetPath;
        _lowestSmallAsset = _levels.LastOrDefault()?.SmallAssetPath;
        ThemeId = theme.Id;

        // Validate and cache every source up front so a broken theme falls back cleanly.
        foreach (var level in _levels)
        {
            LoadSource(level.AssetPath);
            if (level.SmallAssetPath is { } small)
            {
                LoadSource(small);
            }
        }

        if (theme.UnknownAssetPath is { } unknown)
        {
            LoadSource(unknown);
        }

        if (theme.UnknownSmallAssetPath is { } unknownSmall)
        {
            LoadSource(unknownSmall);
        }

        if (theme.ChargingOverlayPath is { } overlay)
        {
            LoadSource(overlay);
        }
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
        var target = new Drawing.Bitmap(
            size,
            size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using var graphics = Drawing.Graphics.FromImage(target);
        graphics.Clear(Drawing.Color.Transparent);
        graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality;

        var asset = SelectAsset(state.Percent, size);
        graphics.DrawImage(LoadSource(asset), 0, 0, size, size);

        if (state.IsCharging && _theme.ChargingOverlayPath is { } overlay)
        {
            graphics.DrawImage(LoadSource(overlay), 0, 0, size, size);
        }

        return target;
    }

    public void Dispose()
    {
        foreach (var bitmap in _sources.Values)
        {
            bitmap.Dispose();
        }

        _sources.Clear();
    }

    private string SelectAsset(int? percent, int size)
    {
        var preferSmall = size <= 20;
        if (percent is null)
        {
            if (preferSmall && _theme.UnknownSmallAssetPath is { } unknownSmall)
            {
                return unknownSmall;
            }

            return _theme.UnknownAssetPath ?? _lowestAsset
                ?? throw new InvalidOperationException(
                    $"Theme '{_theme.Id}' has no unknown or fallback asset.");
        }

        var level = _levels.FirstOrDefault(candidate => percent >= candidate.MinPercent);
        if (level is null)
        {
            return preferSmall
                ? _lowestSmallAsset ?? _lowestAsset
                    ?? throw new InvalidOperationException(
                        $"Theme '{_theme.Id}' has no asset for {percent}%.")
                : _lowestAsset
                    ?? throw new InvalidOperationException(
                        $"Theme '{_theme.Id}' has no asset for {percent}%.");
        }

        if (preferSmall && level.SmallAssetPath is { } smallAsset)
        {
            return smallAsset;
        }

        return level.AssetPath;
    }

    private Drawing.Bitmap LoadSource(string path)
    {
        if (_sources.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Drawing.Bitmap source;
        if (string.Equals(
                Path.GetExtension(path),
                ".ico",
                StringComparison.OrdinalIgnoreCase))
        {
            using var icon = new Drawing.Icon(path);
            source = icon.ToBitmap();
        }
        else
        {
            using var stream = File.OpenRead(path);
            using var image = Drawing.Image.FromStream(
                stream,
                useEmbeddedColorManagement: false,
                validateImageData: true);
            source = new Drawing.Bitmap(image);
        }

        _sources[path] = source;
        return source;
    }
}
