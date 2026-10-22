namespace TinyBattery.Abstractions.Transports;

public interface IBleGattConnection : IDeviceConnection
{
    Task<byte[]> ReadAsync(Guid serviceUuid, Guid characteristicUuid, CancellationToken ct);
}
