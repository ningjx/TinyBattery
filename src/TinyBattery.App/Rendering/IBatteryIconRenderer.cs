using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public interface IBatteryIconRenderer : IDisposable
{
    string ThemeId { get; }

    Drawing.Icon Render(BatteryIconFrame frame, int size);

    Drawing.Bitmap RenderBitmap(BatteryIconFrame frame, int size);
}

public static class BatteryIconRendererExtensions
{
    public static Drawing.Icon Render(
        this IBatteryIconRenderer renderer,
        BatteryIconState state,
        int size) =>
        renderer.Render(BatteryIconFrame.Single(state), size);

    public static Drawing.Bitmap RenderBitmap(
        this IBatteryIconRenderer renderer,
        BatteryIconState state,
        int size) =>
        renderer.RenderBitmap(BatteryIconFrame.Single(state), size);
}
