using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.Transports;

public interface IDeviceTransport
{
    TransportKind Kind { get; }

    Task<IReadOnlyList<DeviceCandidate>> DiscoverAsync(CancellationToken ct);

    Task<IDeviceConnection> OpenAsync(DeviceCandidate candidate, CancellationToken ct);
}
