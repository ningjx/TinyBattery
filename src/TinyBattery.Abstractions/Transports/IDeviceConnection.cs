using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.Transports;

public interface IDeviceConnection : IAsyncDisposable
{
    DeviceCandidate Candidate { get; }

    bool IsOpen { get; }
}
