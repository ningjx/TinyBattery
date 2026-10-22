using TinyBattery.Abstractions.Devices;

namespace TinyBattery.Abstractions.Registry;

public interface IDeviceMatcher
{
    MatchResult? Match(DeviceCandidate candidate);
}
