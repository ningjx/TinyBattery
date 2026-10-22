using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HidSharp;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Infrastructure.Hid;

public sealed class HidSharpTransport : IDeviceTransport
{
    private static readonly Regex InterfaceRegex = new(
        "mi_([0-9a-fA-F]{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public TransportKind Kind => TransportKind.Hid;

    public Task<IReadOnlyList<DeviceCandidate>> DiscoverAsync(CancellationToken ct)
    {
        var candidates = new List<DeviceCandidate>();
        foreach (var device in DeviceList.Local.GetHidDevices())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                candidates.Add(CreateCandidate(device));
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidOperationException)
            {
                // A device can disappear while it is being enumerated.
            }
        }

        return Task.FromResult<IReadOnlyList<DeviceCandidate>>(candidates);
    }

    public Task<IDeviceConnection> OpenAsync(
        DeviceCandidate candidate,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var device = DeviceList.Local.GetHidDevices()
            .FirstOrDefault(item =>
                string.Equals(
                    item.DevicePath,
                    candidate.InterfacePath,
                    StringComparison.OrdinalIgnoreCase));
        if (device is null)
        {
            throw new IOException(
                $"HID device '{candidate.ProductName ?? candidate.StableId}' is no longer present.");
        }

        if (!device.TryOpen(out var stream))
        {
            throw new UnauthorizedAccessException(
                $"Could not open HID device '{candidate.ProductName ?? candidate.StableId}'. "
                + "It may be exclusively held by another application.");
        }

        stream.ReadTimeout = 1000;
        stream.WriteTimeout = 1000;
        return Task.FromResult<IDeviceConnection>(
            new HidSharpConnection(device, stream, candidate));
    }

    private static DeviceCandidate CreateCandidate(HidDevice device)
    {
        var caps = NativeHidInfo.TryGetCaps(device.DevicePath);
        var interfaceName = GetInterfaceName(device.DevicePath);
#pragma warning disable CS0612 // Name properties are obsolete but still the only string accessors.
        var serial = TryGet(() => device.SerialNumber);
        var manufacturer = TryGet(() => device.Manufacturer);
#pragma warning restore CS0612
        var product = TryGet(() => device.GetFriendlyName());
        var stableId = StableId(device, interfaceName, serial);

        return new DeviceCandidate(
            stableId,
            TransportKind.Hid,
            (ushort)device.VendorID,
            (ushort)device.ProductID,
            product,
            manufacturer,
            serial,
            device.DevicePath,
            caps?.UsagePage,
            caps?.Usage,
            interfaceName,
            caps?.InputReportByteLength ?? device.GetMaxInputReportLength(),
            caps?.OutputReportByteLength ?? device.GetMaxOutputReportLength());
    }

    private static string StableId(
        HidDevice device,
        string? interfaceName,
        string? serial)
    {
        var identity = $"{device.VendorID:X4}:{device.ProductID:X4}:"
            + $"{interfaceName ?? "MI_00"}:{serial ?? device.DevicePath}";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(identity)))[..16];
        return $"{device.VendorID:X4}:{device.ProductID:X4}:{interfaceName ?? "MI_00"}:{hash}";
    }

    private static string? GetInterfaceName(string devicePath)
    {
        var match = InterfaceRegex.Match(devicePath);
        return match.Success ? $"MI_{match.Groups[1].Value.ToUpperInvariant()}" : null;
    }

    private static string? TryGet(Func<string?> getter)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
