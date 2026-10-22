namespace TinyBattery.Abstractions.Plugins;

public interface IPluginLoader
{
    Task LoadAsync(CancellationToken ct);
}
