using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Abstractions.Protocols;

public sealed record ProtocolContext(
    DeviceCandidate Candidate,
    IDeviceConnection Connection,
    DeviceDefinition Definition);
