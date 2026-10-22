namespace TinyBattery.Abstractions.Devices;

public sealed record BatteryReading(
    int? Percent,
    double? VoltageMv,
    ChargingState Charging,
    DateTimeOffset Timestamp,
    string? RawHex = null)
{
    public bool HasPercent => Percent is >= 0 and <= 100;
}
