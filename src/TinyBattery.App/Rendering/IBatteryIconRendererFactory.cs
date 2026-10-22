using TinyBattery.Abstractions.Themes;

namespace TinyBattery.App.Rendering;

public interface IBatteryIconRendererFactory
{
    IBatteryIconRenderer Create(IconThemeDescriptor theme);
}
