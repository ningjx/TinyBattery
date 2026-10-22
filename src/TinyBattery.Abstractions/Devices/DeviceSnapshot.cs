namespace TinyBattery.Abstractions.Devices;

public sealed record DeviceSnapshot(
    string LogicalDeviceId,
    string DisplayName,
    DeviceConnectionState State,
    BatteryReading? Battery,
    DateTimeOffset? LastSuccess,
    string? LastError,
    bool IsStale);
