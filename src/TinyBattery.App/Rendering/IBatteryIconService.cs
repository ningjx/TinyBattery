using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public interface IBatteryIconService : IDisposable
{
    string ThemeId { get; }

    int TrayIconSize { get; }

    int SlotCount { get; }

    IReadOnlyList<IconThemeSlot> Slots { get; }

    Drawing.Icon GetIcon(BatteryIconState state);

    Drawing.Icon GetIcon(BatteryIconState state, int size);

    Drawing.Icon GetIcon(BatteryIconFrame frame);

    Drawing.Icon GetIcon(BatteryIconFrame frame, int size);

    Drawing.Icon GetDefaultIcon();

    Drawing.Icon GetDefaultIcon(int size);

    event EventHandler? ThemeChanged;
}
