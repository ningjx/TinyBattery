using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Themes;
using TinyBattery.App.Tray;

namespace TinyBattery.App.Tests;

public sealed class TrayDisplaySelectorTests
{
    [Fact]
    public void NoSnapshots_ReturnsNull()
    {
        Assert.Null(TrayDisplaySelector.Select([], null));
    }

    [Fact]
    public void SnapshotWithoutBattery_ReturnsNull()
    {
        var snapshot = Snapshot("device", percent: null);

        Assert.Null(TrayDisplaySelector.Select([snapshot], null));
    }

    [Fact]
    public void PinnedMissing_ReturnsNull()
    {
        var snapshot = Snapshot("device", 80);

        Assert.Null(TrayDisplaySelector.Select([snapshot], "missing"));
    }

    [Fact]
    public void PinnedPresent_IsSelected()
    {
        var first = Snapshot("first", 80);
        var second = Snapshot("second", 40);

        var selected = TrayDisplaySelector.Select([first, second], "first");

        Assert.Same(first, selected);
    }

    [Fact]
    public void LowestBattery_IsSelectedByDefault()
    {
        var first = Snapshot("first", 80);
        var second = Snapshot("second", 40);

        var selected = TrayDisplaySelector.Select([first, second], null);

        Assert.Same(second, selected);
    }

    [Theory]
    [InlineData("fallback", true)]
    [InlineData("classic", false)]
    public void DefaultIconRules(string themeId, bool expectedWithDevice)
    {
        var snapshot = Snapshot("device", 80);

        Assert.Equal(
            expectedWithDevice,
            TrayDisplaySelector.ShouldUseDefaultIcon(themeId, snapshot));
        Assert.True(TrayDisplaySelector.ShouldUseDefaultIcon(themeId, null));
    }

    private static DeviceSnapshot Snapshot(string id, int? percent)
    {
        var battery = percent is null
            ? null
            : new BatteryReading(
                percent,
                null,
                ChargingState.Discharging,
                DateTimeOffset.UtcNow);

        return new DeviceSnapshot(
            id,
            "Device",
            DeviceConnectionState.Connected,
            battery,
            battery?.Timestamp,
            null,
            IsStale: false);
    }
}
