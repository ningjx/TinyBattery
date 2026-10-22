using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.State;

public interface IBatteryStateStore
{
    IReadOnlyList<DeviceSnapshot> Snapshot();

    event EventHandler<DeviceSnapshot>? Updated;

    void Update(DeviceSnapshot snapshot);

    void Remove(string logicalDeviceId);
}
