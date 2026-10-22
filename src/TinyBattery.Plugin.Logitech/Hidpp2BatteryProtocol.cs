using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Transports;

namespace TinyBattery.Plugin.Logitech;

/// <summary>
/// Read-only HID++ 2.0 battery probe. Only IRoot.GetFeature and battery
/// feature reads are sent; this class must never write configuration.
/// </summary>
public sealed class Hidpp2BatteryProtocol : IBatteryProtocol
{
    private const byte ShortReportId = 0x10;
    private const byte LongReportId = 0x11;
    private const byte ErrorFeatureIndex = 0xFF;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger<Hidpp2BatteryProtocol> _logger;

    public Hidpp2BatteryProtocol(ILogger<Hidpp2BatteryProtocol> logger)
    {
        _logger = logger;
    }

    public string Kind => "logitech.hidpp2";

    public async Task<IReadOnlyList<ProtocolReading>> ReadAsync(
        ProtocolContext context,
        CancellationToken ct)
    {
        if (context.Connection is not IHidConnection hid)
        {
            throw new ProtocolException(
                $"{Kind} requires an HID connection, but received {context.Connection.GetType().Name}.");
        }

        var options = JsonSerializer.Deserialize<Hidpp2Options>(
            context.Definition.Protocol.Options.GetRawText(),
            JsonOptions)
            ?? throw new ProtocolException(
                $"Protocol options for '{context.Definition.Id}' produced an empty result.");

        Validate(options);

        var timeout = TimeSpan.FromSeconds(
            Math.Max(1, context.Definition.Polling.TimeoutSeconds));
        var results = new List<(byte DeviceIndex, BatteryReading Reading)>();

        foreach (var rawIndex in options.DeviceIndexes)
        {
            ct.ThrowIfCancellationRequested();
            var deviceIndex = (byte)rawIndex;
            var reading = await TryReadDeviceAsync(
                hid,
                deviceIndex,
                options,
                timeout,
                ct);
            if (reading is null)
            {
                continue;
            }

            results.Add((deviceIndex, reading));
            if (!options.ScanAllDeviceIndexes)
            {
                break;
            }
        }

        if (results.Count == 0)
        {
            throw new ProtocolException(
                $"No HID++ battery response from '{context.Definition.DisplayName}'. "
                + "Press a key to wake the device and check that its receiver is connected.");
        }

        var multiple = results.Count > 1;
        return results
            .Select(result => new ProtocolReading(
                $"{context.Candidate.StableId}/{result.DeviceIndex}",
                multiple
                    ? $"{context.Definition.DisplayName} #{result.DeviceIndex}"
                    : context.Definition.DisplayName,
                result.Reading))
            .ToArray();
    }

    private async Task<BatteryReading?> TryReadDeviceAsync(
        IHidConnection hid,
        byte deviceIndex,
        Hidpp2Options options,
        TimeSpan timeout,
        CancellationToken ct)
    {
        foreach (var featureText in options.BatteryFeatures)
        {
            ct.ThrowIfCancellationRequested();
            var featureId = ParseFeature(featureText);
            if (featureId is null)
            {
                _logger.LogWarning(
                    "Ignoring invalid HID++ battery feature '{Feature}'.",
                    featureText);
                continue;
            }

            var featureIndex = await GetFeatureIndexAsync(
                hid,
                deviceIndex,
                featureId.Value,
                options.SoftwareId,
                timeout,
                ct);
            if (featureIndex is null or 0)
            {
                continue;
            }

            _logger.LogDebug(
                "HID++ device {DeviceIndex} exposes feature 0x{FeatureId:X4} at index 0x{FeatureIndex:X2}.",
                deviceIndex,
                featureId.Value,
                featureIndex.Value);

            var reading = featureId.Value switch
            {
                0x1001 => await ReadVoltageAsync(
                    hid, deviceIndex, featureIndex.Value, options.SoftwareId, timeout, ct),
                0x1000 => await ReadStatusAsync(
                    hid, deviceIndex, featureIndex.Value, options.SoftwareId, timeout, ct),
                0x1004 => await ReadUnifiedAsync(
                    hid, deviceIndex, featureIndex.Value, options.SoftwareId, timeout, ct),
                _ => null,
            };

            if (reading is not null)
            {
                return reading;
            }
        }

        return null;
    }

    private async Task<byte?> GetFeatureIndexAsync(
        IHidConnection hid,
        byte deviceIndex,
        int featureId,
        int softwareId,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var functionByte = FunctionByte(0, softwareId);
        var request = BuildLong(
            deviceIndex,
            featureIndex: 0x00,
            functionByte,
            (byte)(featureId >> 8),
            (byte)(featureId & 0xFF));

        var response = await ExchangeAsync(
            hid,
            request,
            deviceIndex,
            featureIndex: 0x00,
            functionByte,
            timeout,
            ct);

        return response is { Length: >= 5 } ? response[4] : null;
    }

    private async Task<BatteryReading?> ReadVoltageAsync(
        IHidConnection hid,
        byte deviceIndex,
        byte featureIndex,
        int softwareId,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var functionByte = FunctionByte(0, softwareId);
        var response = await ExchangeAsync(
            hid,
            BuildLong(deviceIndex, featureIndex, functionByte),
            deviceIndex,
            featureIndex,
            functionByte,
            timeout,
            ct);

        if (response is not { Length: >= 7 })
        {
            return null;
        }

        var millivolts = (response[4] << 8) | response[5];
        var flags = response[6];
        int? percent = millivolts >= 2000
            ? VoltageToPercent.Estimate(millivolts)
            : null;

        return new BatteryReading(
            percent,
            millivolts,
            MapVoltageFlags(flags),
            DateTimeOffset.UtcNow,
            Convert.ToHexString(response));
    }

    private async Task<BatteryReading?> ReadStatusAsync(
        IHidConnection hid,
        byte deviceIndex,
        byte featureIndex,
        int softwareId,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var functionByte = FunctionByte(0, softwareId);
        var response = await ExchangeAsync(
            hid,
            BuildLong(deviceIndex, featureIndex, functionByte),
            deviceIndex,
            featureIndex,
            functionByte,
            timeout,
            ct);

        if (response is not { Length: >= 7 })
        {
            return null;
        }

        var rawPercent = response[4];
        var status = response[6];
        int? percent = rawPercent <= 100 ? rawPercent : null;
        if (rawPercent == 0 && status != 0)
        {
            percent = null;
        }

        return new BatteryReading(
            percent,
            null,
            MapStatus(status),
            DateTimeOffset.UtcNow,
            Convert.ToHexString(response));
    }

    private async Task<BatteryReading?> ReadUnifiedAsync(
        IHidConnection hid,
        byte deviceIndex,
        byte featureIndex,
        int softwareId,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var functionByte = FunctionByte(1, softwareId);
        var response = await ExchangeAsync(
            hid,
            BuildLong(deviceIndex, featureIndex, functionByte),
            deviceIndex,
            featureIndex,
            functionByte,
            timeout,
            ct);

        if (response is not { Length: >= 7 })
        {
            return null;
        }

        var rawPercent = response[4];
        var level = response[5];
        var status = response[6];
        int? percent = rawPercent is > 0 and <= 100
            ? rawPercent
            : level switch
            {
                1 => 10,
                2 => 25,
                4 => 60,
                8 => 100,
                _ => null,
            };

        return new BatteryReading(
            percent,
            null,
            MapStatus(status),
            DateTimeOffset.UtcNow,
            Convert.ToHexString(response));
    }

    private async Task<byte[]?> ExchangeAsync(
        IHidConnection hid,
        byte[] request,
        byte deviceIndex,
        byte featureIndex,
        byte functionByte,
        TimeSpan timeout,
        CancellationToken ct)
    {
        await hid.WriteAsync(request, ct);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var response = await hid.ReadAsync(remaining, ct);
            if (response is not { Length: >= 4 })
            {
                continue;
            }

            if (response[0] is not (ShortReportId or LongReportId))
            {
                continue;
            }

            if (response[1] != deviceIndex)
            {
                continue;
            }

            if (response[3] != functionByte)
            {
                continue;
            }

            if (response[2] == ErrorFeatureIndex)
            {
                _logger.LogDebug(
                    "HID++ error response for feature index 0x{FeatureIndex:X2} on device {DeviceIndex}.",
                    featureIndex,
                    deviceIndex);
                return null;
            }

            if (response[2] != featureIndex)
            {
                continue;
            }

            return response;
        }

        _logger.LogDebug(
            "HID++ request to device {DeviceIndex}, feature 0x{FeatureIndex:X2} timed out.",
            deviceIndex,
            featureIndex);
        return null;
    }

    private static byte[] BuildLong(
        byte deviceIndex,
        byte featureIndex,
        byte functionByte,
        params byte[] parameters)
    {
        var report = new byte[20];
        report[0] = LongReportId;
        report[1] = deviceIndex;
        report[2] = featureIndex;
        report[3] = functionByte;
        var count = Math.Min(parameters.Length, 16);
        Array.Copy(parameters, 0, report, 4, count);
        return report;
    }

    private static byte FunctionByte(int function, int softwareId) =>
        (byte)(((function & 0x0F) << 4) | (softwareId & 0x0F));

    private static int? ParseFeature(string text)
    {
        var value = text.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return int.TryParse(
            value,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out var feature)
            && feature is >= 0 and <= 0xFFFF
            ? feature
            : null;
    }

    private static void Validate(Hidpp2Options options)
    {
        if (options.SoftwareId is < 1 or > 15)
        {
            throw new ProtocolException("softwareId must be between 1 and 15.");
        }

        if (options.DeviceIndexes.Length == 0)
        {
            throw new ProtocolException("deviceIndexes must not be empty.");
        }

        if (options.DeviceIndexes.Any(index => index is < 0 or > 255))
        {
            throw new ProtocolException("deviceIndexes must be between 0 and 255.");
        }

        if (options.BatteryFeatures.Length == 0)
        {
            throw new ProtocolException("batteryFeatures must not be empty.");
        }
    }

    private static ChargingState MapStatus(byte status) => status switch
    {
        0 => ChargingState.Discharging,
        1 or 2 or 4 => ChargingState.Charging,
        3 => ChargingState.Full,
        5 or 6 or 7 => ChargingState.Error,
        _ => ChargingState.Unknown,
    };

    private static ChargingState MapVoltageFlags(byte flags)
    {
        if ((flags & 0x80) == 0)
        {
            return ChargingState.Discharging;
        }

        return (flags & 0x03) switch
        {
            1 => ChargingState.Full,
            2 => ChargingState.Error,
            _ => ChargingState.Charging,
        };
    }
}
