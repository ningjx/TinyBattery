using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public interface IBatteryIconRenderer : IDisposable
{
    string ThemeId { get; }

    Drawing.Icon Render(BatteryIconState state, int size);

    Drawing.Bitmap RenderBitmap(BatteryIconState state, int size);
}
