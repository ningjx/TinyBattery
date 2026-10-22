namespace TinyBattery.Abstractions.Devices;

public sealed record ProtocolReading(
    string LogicalDeviceId,
    string DisplayName,
    BatteryReading Battery);
