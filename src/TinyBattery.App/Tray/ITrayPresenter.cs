using TinyBattery.Abstractions.Themes;

namespace TinyBattery.App.Tray;

public interface ITrayPresenter : IDisposable
{
    void Start();

    void SetBattery(BatteryIconState state, string toolTip);

    void Refresh();
}
