using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public sealed class BatteryPalette
{
    public static readonly Drawing.Color HealthyFill = Drawing.Color.FromArgb(0x45, 0xC9, 0x6B);
    public static readonly Drawing.Color WarningFill = Drawing.Color.FromArgb(0xD9, 0x77, 0x06);
    public static readonly Drawing.Color CriticalFill = Drawing.Color.FromArgb(0xC4, 0x2B, 0x1C);
    public static readonly Drawing.Color UnknownFill = Drawing.Color.FromArgb(0x7A, 0x7A, 0x7A);
    public static readonly Drawing.Color Shell = Drawing.Color.Black;

    public static BatteryPalette Default { get; } = new(new ClassicIconThemeOptions());

    private readonly ClassicIconThemeOptions _options;
    private readonly Drawing.Color _healthy;
    private readonly Drawing.Color _warning;
    private readonly Drawing.Color _critical;
    private readonly Drawing.Color _unknown;

    public BatteryPalette(ClassicIconThemeOptions options)
    {
        _options = options;
        _healthy = ParseColor(options.HealthyColor, HealthyFill);
        _warning = ParseColor(options.WarningColor, WarningFill);
        _critical = ParseColor(options.CriticalColor, CriticalFill);
        _unknown = ParseColor(options.UnknownColor, UnknownFill);
    }

    public Drawing.Color GetColor(int? percent)
    {
        if (percent is null)
        {
            return _unknown;
        }

        var value = percent.Value;
        if (value >= _options.HealthyThreshold)
        {
            return _healthy;
        }

        if (value >= _options.WarningThreshold)
        {
            return _warning;
        }

        return value >= 0 ? _critical : _unknown;
    }

    public float GetRatio(int? percent) =>
        percent is int value
            ? Math.Clamp(value, 0, 100) / 100f
            : (float)Math.Clamp(_options.UnknownFillRatio, 0, 1);

    public static Drawing.Color GetFillColor(int? percent) =>
        Default.GetColor(percent);

    public static float GetFillRatio(int? percent) =>
        Default.GetRatio(percent);

    private static Drawing.Color ParseColor(string value, Drawing.Color fallback)
    {
        try
        {
            return Drawing.ColorTranslator.FromHtml(value);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
