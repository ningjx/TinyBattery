namespace TinyBattery.Abstractions.Themes;

public enum IconFillMode
{
    Rect,
    Ring,
    Sector,
    Tint,
}

public sealed record IconThemeSlot(
    string Id,
    string? Label,
    IconFillMode Mode,
    string Target);

public sealed record ThemeColorSet(
    string Healthy = "#2F9E64",
    string Warning = "#C08A1E",
    string Critical = "#C24B4B",
    string Unknown = "#8C8C8C",
    string? Track = null);

public sealed record SvgThemeOptions(
    string SvgPath,
    IReadOnlyList<IconThemeSlot> Slots,
    ThemeColorSet Colors,
    string? ChargingOverlayPath = null,
    IReadOnlyDictionary<string, string>? StateOverrides = null,
    double StartAngle = -90,
    bool Clockwise = true,
    double InnerRadius = 0,
    string FillDirection = "vertical");
