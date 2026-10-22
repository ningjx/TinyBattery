using System.Collections.Concurrent;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.State;

namespace TinyBattery.Core.State;

public sealed class InMemoryBatteryStateStore : IBatteryStateStore
{
    private readonly ConcurrentDictionary<string, DeviceSnapshot> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<DeviceSnapshot>? Updated;

    public IReadOnlyList<DeviceSnapshot> Snapshot() =>
        _snapshots.Values
            .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.LogicalDeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public void Update(DeviceSnapshot snapshot)
    {
        _snapshots[snapshot.LogicalDeviceId] = snapshot;
        Updated?.Invoke(this, snapshot);
    }

    public void Remove(string logicalDeviceId)
    {
        if (_snapshots.TryRemove(logicalDeviceId, out var snapshot))
        {
            Updated?.Invoke(this, snapshot);
        }
    }
}
