using TinyBattery.Abstractions.Devices;
using TinyBattery.App.Tray;

namespace TinyBattery.App.Tests;

public sealed class MultiSlotTrayDisplayTests
{
    [Fact]
    public void TwoSlots_AutoFillsTwoDevices()
    {
        var first = Snapshot("first", "First", 80);
        var second = Snapshot("second", "Second", 40);

        var result = TrayDisplaySelector.Resolve(
            [first, second],
            [],
            slotCount: 2,
            themeAvailable: true);

        Assert.False(result.UseDefaultIcon);
        Assert.Same(first, result.Slots[0]);
        Assert.Same(second, result.Slots[1]);
    }

    [Fact]
    public void ExplicitAssignment_KeepsSlotOrder()
    {
        var first = Snapshot("first", "First", 80);
        var second = Snapshot("second", "Second", 40);

        var result = TrayDisplaySelector.Resolve(
            [first, second],
            ["second"],
            slotCount: 2,
            themeAvailable: true);

        Assert.Same(second, result.Slots[0]);
        Assert.Same(first, result.Slots[1]);
    }

    [Fact]
    public void MissingExplicitDevice_LeavesSlotEmpty()
    {
        var first = Snapshot("first", "First", 80);

        var result = TrayDisplaySelector.Resolve(
            [first],
            ["missing"],
            slotCount: 2,
            themeAvailable: true);

        Assert.Null(result.Slots[0]);
        Assert.Same(first, result.Slots[1]);
    }

    [Fact]
    public void NoTheme_UsesDefaultIcon()
    {
        var first = Snapshot("first", "First", 80);

        var result = TrayDisplaySelector.Resolve(
            [first],
            [],
            slotCount: 2,
            themeAvailable: false);

        Assert.True(result.UseDefaultIcon);
        Assert.All(result.Slots, slot => Assert.Null(slot));
    }

    [Fact]
    public void SingleSlot_WithoutAssignment_UsesLowestBattery()
    {
        var first = Snapshot("first", "First", 80);
        var second = Snapshot("second", "Second", 40);

        var result = TrayDisplaySelector.Resolve(
            [first, second],
            [],
            slotCount: 1,
            themeAvailable: true);

        Assert.Same(second, result.Slots[0]);
    }

    private static DeviceSnapshot Snapshot(string id, string name, int percent)
    {
        var battery = new BatteryReading(
            percent,
            null,
            ChargingState.Discharging,
            DateTimeOffset.UtcNow);
        return new DeviceSnapshot(
            id,
            name,
            DeviceConnectionState.Connected,
            battery,
            battery.Timestamp,
            null,
            IsStale: false);
    }
}
