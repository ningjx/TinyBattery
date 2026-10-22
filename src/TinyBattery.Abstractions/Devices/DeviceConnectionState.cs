namespace TinyBattery.Abstractions.Devices;

public enum DeviceConnectionState
{
    Discovered,
    Opening,
    Connected,
    Asleep,
    Unreachable,
    AccessDenied,
    Faulted,
    Disabled,
}
