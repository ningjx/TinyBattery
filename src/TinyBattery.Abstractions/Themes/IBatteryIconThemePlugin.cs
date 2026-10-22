using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Abstractions.Themes;

public interface IBatteryIconThemePlugin
{
    IconThemeDescriptor Describe();

    IconAsset Render(BatteryIconState state, int size);
}

public sealed record IconAsset(
    byte[] Data,
    string MediaType,
    int Width,
    int Height);
