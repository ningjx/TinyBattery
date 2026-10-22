namespace TinyBattery.Abstractions.Themes;

public readonly record struct BatteryIconState(
    int? Percent,
    bool IsCharging = false);
