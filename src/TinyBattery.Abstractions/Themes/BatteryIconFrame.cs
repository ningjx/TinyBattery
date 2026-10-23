namespace TinyBattery.Abstractions.Themes;

public readonly record struct BatteryIconSlotState(
    string SlotId,
    int? Percent,
    bool IsCharging,
    bool IsAvailable)
{
    public bool HasReading => IsAvailable && Percent is not null;
}

public sealed record BatteryIconFrame(IReadOnlyList<BatteryIconSlotState> Slots)
{
    public static BatteryIconFrame Single(BatteryIconState state) =>
        new([new BatteryIconSlotState("main", state.Percent, state.IsCharging, true)]);

    public static BatteryIconFrame Empty(string slotId = "main") =>
        new([new BatteryIconSlotState(slotId, null, false, false)]);

    public bool HasReading => Slots.Any(slot => slot.HasReading);
}
