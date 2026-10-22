namespace TinyBattery.Abstractions.Themes;

public sealed record IconThemeLevel(
    int MinPercent,
    string AssetPath,
    string? SmallAssetPath = null);
