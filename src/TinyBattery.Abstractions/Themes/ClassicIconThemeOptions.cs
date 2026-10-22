namespace TinyBattery.Abstractions.Themes;

public sealed record ClassicIconThemeOptions(
    int HealthyThreshold = 60,
    int WarningThreshold = 20,
    string HealthyColor = "#45C96B",
    string WarningColor = "#D97706",
    string CriticalColor = "#C42B1C",
    string UnknownColor = "#7A7A7A",
    double UnknownFillRatio = 0.35);
