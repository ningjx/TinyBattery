using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public sealed class BatteryIconCache : IDisposable
{
    private readonly IBatteryIconRenderer _renderer;
    private readonly Dictionary<(BatteryIconState State, int Size), Drawing.Icon> _icons = [];

    public BatteryIconCache(IBatteryIconRenderer renderer)
    {
        _renderer = renderer;
    }

    public Drawing.Icon Get(BatteryIconState state, int size)
    {
        var key = (state, size);
        if (_icons.TryGetValue(key, out var icon))
        {
            return icon;
        }

        icon = _renderer.Render(state, size);
        _icons[key] = icon;
        return icon;
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
        _renderer.Dispose();
    }
}
