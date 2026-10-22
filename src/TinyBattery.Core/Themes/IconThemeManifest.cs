namespace TinyBattery.Core.Themes;

public sealed class IconThemeManifest
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Version { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public string Kind { get; init; } = "classic";

    public int Size { get; init; } = 32;

    public string? Preview { get; init; }

    public string? AppIcon { get; init; }

    public ClassicThemeManifest? Classic { get; init; }

    public List<ThemeLevelManifest>? Levels { get; init; }

    public string? Unknown { get; init; }

    public string? UnknownSmall { get; init; }

    public string? ChargingOverlay { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class ClassicThemeManifest
{
    public int? HealthyThreshold { get; init; }

    public int? WarningThreshold { get; init; }

    public string? HealthyColor { get; init; }

    public string? WarningColor { get; init; }

    public string? CriticalColor { get; init; }

    public string? UnknownColor { get; init; }

    public double? UnknownFillRatio { get; init; }
}

public sealed class ThemeLevelManifest
{
    public int MinPercent { get; init; }

    public string Asset { get; init; } = "";

    public string? SmallAsset { get; init; }
}
