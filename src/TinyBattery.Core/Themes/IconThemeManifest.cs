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

    public int? SlotCount { get; init; }

    public SvgRenderManifest? Render { get; init; }

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

public sealed class SvgRenderManifest
{
    public string Svg { get; init; } = "";

    public List<SvgSlotManifest>? Slots { get; init; }

    public SvgColorManifest? Colors { get; init; }

    public double? StartAngle { get; init; }

    public bool? Clockwise { get; init; }

    public double? InnerRadius { get; init; }

    public string? FillDirection { get; init; }

    public Dictionary<string, string>? States { get; init; }
}

public sealed class SvgSlotManifest
{
    public string Id { get; init; } = "";

    public string? Label { get; init; }

    public string Mode { get; init; } = "rect";

    public string Target { get; init; } = "";
}

public sealed class SvgColorManifest
{
    public string Healthy { get; init; } = "#2F9E64";

    public string Warning { get; init; } = "#C08A1E";

    public string Critical { get; init; } = "#C24B4B";

    public string Unknown { get; init; } = "#8C8C8C";

    public string? Track { get; init; }
}
