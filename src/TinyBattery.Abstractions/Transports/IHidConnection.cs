namespace TinyBattery.Abstractions.Transports;

public interface IHidConnection : IDeviceConnection
{
    Task WriteAsync(ReadOnlyMemory<byte> report, CancellationToken ct);

    Task<byte[]?> ReadAsync(TimeSpan timeout, CancellationToken ct);

    Task<byte[]> GetFeatureAsync(byte reportId, int length, CancellationToken ct);

    Task SetFeatureAsync(ReadOnlyMemory<byte> report, CancellationToken ct);
}
