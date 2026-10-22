namespace TinyBattery.Abstractions.Devices;

public sealed record DeviceMatch(
    TransportKind Transport,
    ushort? VendorId = null,
    ushort? ProductId = null,
    ushort? UsagePage = null,
    ushort? Usage = null,
    string? Interface = null,
    int? InputReportLength = null,
    int? OutputReportLength = null,
    string? ProductRegex = null,
    string? ManufacturerRegex = null,
    string? SerialRegex = null,
    string? ServiceUuid = null,
    string? DeviceClass = null);
