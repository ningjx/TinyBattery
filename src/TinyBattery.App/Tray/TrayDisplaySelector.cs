using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.App.Tray;

public sealed record TrayDisplayResult(
    IReadOnlyList<DeviceSnapshot?> Slots,
    bool UseDefaultIcon);

public static class TrayDisplaySelector
{
    public static TrayDisplayResult Resolve(
        IReadOnlyList<DeviceSnapshot> snapshots,
        IReadOnlyList<string> displayDeviceIds,
        int slotCount,
        bool themeAvailable)
    {
        slotCount = Math.Clamp(slotCount, 1, 8);
        var slots = new DeviceSnapshot?[slotCount];
        if (!themeAvailable)
        {
            return new TrayDisplayResult(slots, true);
        }

        var byId = snapshots.ToDictionary(
            snapshot => snapshot.LogicalDeviceId,
            StringComparer.OrdinalIgnoreCase);
        var readable = snapshots
            .Where(IsReadable)
            .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < slotCount; index++)
        {
            if (index < displayDeviceIds.Count
                && !string.IsNullOrWhiteSpace(displayDeviceIds[index])
                && byId.TryGetValue(displayDeviceIds[index], out var assigned))
            {
                slots[index] = assigned;
                used.Add(assigned.LogicalDeviceId);
            }
        }

        if (displayDeviceIds.All(string.IsNullOrWhiteSpace))
        {
            if (slotCount == 1)
            {
                slots[0] = readable
                    .OrderBy(snapshot => snapshot.Battery!.Percent ?? int.MaxValue)
                    .ThenBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }
            else
            {
                for (var index = 0; index < Math.Min(slotCount, readable.Length); index++)
                {
                    slots[index] = readable[index];
                    used.Add(readable[index].LogicalDeviceId);
                }
            }
        }
        else
        {
            var available = readable
                .Where(snapshot => !used.Contains(snapshot.LogicalDeviceId))
                .ToArray();
            var availableIndex = 0;
            for (var index = 0; index < slotCount; index++)
            {
                if (slots[index] is not null)
                {
                    continue;
                }

                var hasExplicitId = index < displayDeviceIds.Count
                    && !string.IsNullOrWhiteSpace(displayDeviceIds[index]);
                if (hasExplicitId)
                {
                    // Keep a disconnected explicit assignment empty instead of shifting devices.
                    continue;
                }

                if (availableIndex < available.Length)
                {
                    slots[index] = available[availableIndex++];
                }
            }
        }

        var hasReadable = slots.Any(snapshot => snapshot is not null && IsReadable(snapshot));
        return new TrayDisplayResult(slots, !hasReadable);
    }

    public static DeviceSnapshot? Select(
        IReadOnlyList<DeviceSnapshot> snapshots,
        string? primaryDeviceId)
    {
        var displayable = snapshots.Where(IsReadable).ToArray();
        if (displayable.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(primaryDeviceId))
        {
            return displayable.FirstOrDefault(snapshot =>
                string.Equals(
                    snapshot.LogicalDeviceId,
                    primaryDeviceId,
                    StringComparison.OrdinalIgnoreCase));
        }

        return displayable
            .OrderBy(snapshot => snapshot.Battery!.Percent ?? int.MaxValue)
            .ThenBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    public static bool ShouldUseDefaultIcon(
        string? themeId,
        DeviceSnapshot? selected) =>
        selected is null
        || string.Equals(
            themeId,
            IconThemeDescriptor.FallbackId,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsReadable(DeviceSnapshot snapshot) =>
        snapshot.Battery is { } battery
        && (battery.Percent is not null || battery.VoltageMv is not null);
}
