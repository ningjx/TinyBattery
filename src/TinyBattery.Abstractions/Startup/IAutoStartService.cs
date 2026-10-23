namespace TinyBattery.Abstractions.Startup;

public interface IAutoStartService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    void EnsureEnabled();
}
