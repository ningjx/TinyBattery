using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.Protocols;

public interface IBatteryProtocol
{
    string Kind { get; }

    Task<IReadOnlyList<ProtocolReading>> ReadAsync(
        ProtocolContext context,
        CancellationToken ct);
}
