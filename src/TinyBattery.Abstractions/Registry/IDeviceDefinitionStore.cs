using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.Registry;

public interface IDeviceDefinitionStore
{
    IReadOnlyList<DeviceDefinition> Definitions { get; }

    IReadOnlyList<DefinitionDiagnostic> Diagnostics { get; }

    event EventHandler? Changed;

    Task ReloadAsync(CancellationToken ct);
}
