namespace TinyBattery.Abstractions.Devices;

public sealed record DeviceDefinition(
    int SchemaVersion,
    string Id,
    string DisplayName,
    string? Vendor,
    DeviceMatch? Match,
    ProtocolDefinition Protocol,
    PollingOptions Polling,
    bool Enabled = true,
    int Priority = 0,
    Dictionary<string, string>? Metadata = null,
    string? Version = null,
    IReadOnlyList<DeviceMatch>? Matches = null)
{
    public IReadOnlyList<DeviceMatch> EffectiveMatches =>
        Matches is { Count: > 0 }
            ? Matches
            : Match is null
                ? []
                : [Match];
}
