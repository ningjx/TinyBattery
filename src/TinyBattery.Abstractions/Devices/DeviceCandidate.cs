namespace TinyBattery.Abstractions.Devices;

public sealed record DeviceCandidate(
    string StableId,
    TransportKind Transport,
    ushort? VendorId = null,
    ushort? ProductId = null,
    string? ProductName = null,
    string? Manufacturer = null,
    string? SerialNumber = null,
    string? InterfacePath = null,
    ushort? UsagePage = null,
    ushort? Usage = null,
    string? Interface = null,
    int? InputReportLength = null,
    int? OutputReportLength = null,
    string? ServiceUuid = null,
    string? DeviceClass = null);
