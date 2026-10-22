namespace TinyBattery.Abstractions.Devices;

public sealed record MatchResult(
    DeviceDefinition Definition,
    int Score,
    IReadOnlyList<string> Reasons);
