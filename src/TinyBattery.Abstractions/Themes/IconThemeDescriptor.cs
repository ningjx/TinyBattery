namespace TinyBattery.Abstractions.Themes;

public sealed record IconThemeDescriptor(
    string Id,
    string DisplayName,
    string? Version,
    string? Author,
    string? Description,
    IconThemeKind Kind,
    string? SourceDirectory = null,
    string? PreviewPath = null,
    string? AppIconPath = null,
    ClassicIconThemeOptions? Classic = null,
    IReadOnlyList<IconThemeLevel>? Levels = null,
    string? UnknownAssetPath = null,
    string? UnknownSmallAssetPath = null,
    string? ChargingOverlayPath = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    SvgThemeOptions? Svg = null)
{
    public const string FallbackId = "fallback";

    public int SlotCount => Svg?.Slots.Count ?? 1;

    public IReadOnlyList<IconThemeSlot> Slots => Svg?.Slots
        ?? [new IconThemeSlot("main", null, IconFillMode.Tint, "")];

    public static IconThemeDescriptor CreateFallback() => new(
        FallbackId,
        "Fallback",
        null,
        "TinyBattery",
        "Minimal fallback icon used when no theme plugin is loaded.",
        IconThemeKind.Classic,
        Classic: new ClassicIconThemeOptions(
            HealthyColor: "#7A7A7A",
            WarningColor: "#7A7A7A",
            CriticalColor: "#7A7A7A",
            UnknownColor: "#7A7A7A"));
}
