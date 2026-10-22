using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.App.Tray;

public static class TrayDisplaySelector
{
    public static DeviceSnapshot? Select(
        IReadOnlyList<DeviceSnapshot> snapshots,
        string? primaryDeviceId)
    {
        var displayable = snapshots
            .Where(snapshot => snapshot.Battery is { } battery
                && (battery.Percent is not null || battery.VoltageMv is not null))
            .ToArray();
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
}
