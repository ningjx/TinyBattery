namespace TinyBattery.Abstractions.Devices;

public sealed record DeviceDefinition(
    int SchemaVersion,
    string Id,
    string DisplayName,
    string? Vendor,
    DeviceMatch Match,
    ProtocolDefinition Protocol,
    PollingOptions Polling,
    bool Enabled = true,
    int Priority = 0,
    Dictionary<string, string>? Metadata = null);
