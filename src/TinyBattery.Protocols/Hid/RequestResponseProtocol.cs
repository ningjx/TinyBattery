using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Transports;
using TinyBattery.Protocols.Engine;
using TinyBattery.Protocols.Options;
using TinyBattery.Protocols.Serialization;

namespace TinyBattery.Protocols.Hid;

public sealed class RequestResponseProtocol : IBatteryProtocol
{
    private readonly ILogger<RequestResponseProtocol> _logger;

    public RequestResponseProtocol(ILogger<RequestResponseProtocol> logger)
    {
        _logger = logger;
    }

    public string Kind => "hid.request-response";

    public async Task<IReadOnlyList<ProtocolReading>> ReadAsync(
        ProtocolContext context,
        CancellationToken ct)
    {
        if (context.Connection is not IHidConnection hid)
        {
            throw new ProtocolException(
                $"{Kind} requires an HID connection, but received {context.Connection.GetType().Name}.");
        }

        var options = JsonSerializer.Deserialize<RequestResponseOptions>(
            context.Definition.Protocol.Options.GetRawText(),
            ProtocolJson.Options)
            ?? throw new ProtocolException(
                $"Protocol options for '{context.Definition.Id}' produced an empty result.");

        var frame = BuildFrame(options);
        ChecksumCalculator.ValidateFrameLength(frame.Length, options.Request.Checksum ?? new ChecksumSpec());
        if (options.Request.Checksum is { } checksum)
        {
            ChecksumCalculator.Apply(frame, checksum);
        }

        _logger.LogDebug(
            "Writing {Length} bytes to '{Device}': {Frame}",
            frame.Length,
            context.Definition.DisplayName,
            Convert.ToHexString(frame));

        await hid.WriteAsync(frame, ct);

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(options.TimeoutMs);
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var response = await hid.ReadAsync(remaining, ct);
            if (response is null)
            {
                continue;
            }

            if (!Matches(response, options.Response))
            {
                _logger.LogDebug(
                    "Ignoring non-matching response from '{Device}': {Response}",
                    context.Definition.DisplayName,
                    Convert.ToHexString(response));
                continue;
            }

            var reading = Parse(response, options);
            _logger.LogDebug(
                "Read {Percent}% from '{Device}'.",
                reading.Percent,
                context.Definition.DisplayName);

            return
            [
                new ProtocolReading(
                    context.Candidate.StableId,
                    context.Definition.DisplayName,
                    reading),
            ];
        }

        throw new ProtocolException(
            $"Timed out after {options.TimeoutMs} ms waiting for a matching response from '{context.Definition.DisplayName}'.");
    }

    private static byte[] BuildFrame(RequestResponseOptions options)
    {
        if (options.ReportId is < 0 or > 255)
        {
            throw new ProtocolException("reportId must be between 0 and 255.");
        }

        if (options.TimeoutMs is < 100 or > 60000)
        {
            throw new ProtocolException("timeoutMs must be between 100 and 60000.");
        }

        if (options.Request.Payload.Length == 0)
        {
            throw new ProtocolException("request.payload must not be empty.");
        }

        if (options.Response.Match.Count == 0)
        {
            throw new ProtocolException("response.match must contain at least one condition.");
        }

        if (options.Extract.Count == 0)
        {
            throw new ProtocolException("extract must contain at least one field.");
        }

        foreach (var condition in options.Response.Match)
        {
            if (condition.Offset < 0)
            {
                throw new ProtocolException("Response match offsets must not be negative.");
            }

            if (condition.EqualTo is null
                && (condition.Mask is null || condition.Value is null))
            {
                throw new ProtocolException(
                    "Each response match condition needs either 'equals' or 'mask' + 'value'.");
            }
        }

        var prefixLength = options.IncludeReportId ? 1 : 0;
        var frame = new byte[options.Request.Payload.Length + prefixLength];
        if (options.IncludeReportId)
        {
            frame[0] = (byte)options.ReportId;
        }

        for (var index = 0; index < options.Request.Payload.Length; index++)
        {
            var value = options.Request.Payload[index];
            if (value is < 0 or > 255)
            {
                throw new ProtocolException(
                    $"request.payload[{index}] must be between 0 and 255.");
            }

            frame[prefixLength + index] = (byte)value;
        }

        return frame;
    }

    private static bool Matches(ReadOnlySpan<byte> response, ResponseSpec spec)
    {
        if (response.Length < spec.MinLength)
        {
            return false;
        }

        foreach (var condition in spec.Match)
        {
            if (condition.Offset >= response.Length)
            {
                return false;
            }

            var value = response[condition.Offset];
            if (condition.EqualTo is { } equal && value != equal)
            {
                return false;
            }

            if (condition.Mask is { } mask
                && condition.Value is { } expected
                && (value & mask) != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static BatteryReading Parse(byte[] response, RequestResponseOptions options)
    {
        var fields = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, spec) in options.Extract)
        {
            fields[name] = FieldExtractor.Extract(response, spec);
        }

        int? percent = null;
        if (TryGetField(fields, "percent", out var percentValue))
        {
            var rounded = (int)Math.Round(percentValue, MidpointRounding.AwayFromZero);
            if (rounded is < 0 or > 100)
            {
                throw new ProtocolException($"Extracted battery percent {rounded} is outside 0..100.");
            }

            percent = rounded;
        }

        double? voltage = null;
        if (TryGetField(fields, "voltageMv", out var voltageValue)
            || TryGetField(fields, "voltage", out voltageValue))
        {
            voltage = voltageValue;
        }

        var charging = ChargingState.Unknown;
        if (TryGetField(fields, "charging", out var chargingValue)
            && TryGetFieldSpec(options.Extract, "charging", out var chargingSpec)
            && chargingSpec?.Map is { } map)
        {
            var key = ((long)Math.Round(chargingValue, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);
            if (map.TryGetValue(key, out var text)
                && Enum.TryParse<ChargingState>(text, ignoreCase: true, out var parsed))
            {
                charging = parsed;
            }
        }

        return new BatteryReading(
            percent,
            voltage,
            charging,
            DateTimeOffset.UtcNow,
            Convert.ToHexString(response));
    }

    private static bool TryGetField(
        Dictionary<string, double> fields,
        string name,
        out double value)
    {
        if (fields.TryGetValue(name, out value))
        {
            return true;
        }

        foreach (var (key, candidate) in fields)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetFieldSpec(
        Dictionary<string, FieldSpec> fields,
        string name,
        out FieldSpec? spec)
    {
        if (fields.TryGetValue(name, out spec))
        {
            return true;
        }

        foreach (var (key, candidate) in fields)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                spec = candidate;
                return true;
            }
        }

        spec = null;
        return false;
    }
}
