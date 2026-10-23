using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public sealed class BatteryIconCache : IDisposable
{
    private readonly IBatteryIconRenderer _renderer;
    private readonly Dictionary<(string FrameKey, int Size), Drawing.Icon> _icons = [];

    public BatteryIconCache(IBatteryIconRenderer renderer)
    {
        _renderer = renderer;
    }

    public Drawing.Icon Get(BatteryIconFrame frame, int size)
    {
        var key = (FrameKey(frame), size);
        if (_icons.TryGetValue(key, out var icon))
        {
            return icon;
        }

        icon = _renderer.Render(frame, size);
        _icons[key] = icon;
        return icon;
    }

    public Drawing.Icon Get(BatteryIconState state, int size) =>
        Get(BatteryIconFrame.Single(state), size);

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
        _renderer.Dispose();
    }

    private static string FrameKey(BatteryIconFrame frame) =>
        string.Join(
            "|",
            frame.Slots.Select(slot =>
                $"{slot.SlotId}:{(slot.IsAvailable ? 1 : 0)}:"
                + $"{(slot.Percent?.ToString() ?? "-")}:{(slot.IsCharging ? 1 : 0)}"));
}
