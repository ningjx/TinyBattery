using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Transports;
using TinyBattery.Plugin.Logitech;

namespace TinyBattery.Plugin.Logitech.Tests;

public sealed class Hidpp2BatteryProtocolTests
{
    [Fact]
    public async Task ReadsG913Voltage_UsingOnlyReadQueries()
    {
        var definition = LoadDefinition();
        var connection = new FakeHidConnection(Candidate());
        connection.Responder = request =>
        {
            if (IsFeatureQuery(request, 0x1001))
            {
                return LongResponse(0x00, 0x0A, 0x07);
            }

            if (request[2] == 0x07)
            {
                return LongResponse(0x07, 0x0A, 0x0F, 0xAA, 0x00);
            }

            return null;
        };

        var protocol = new Hidpp2BatteryProtocol(
            NullLogger<Hidpp2BatteryProtocol>.Instance);
        var readings = await protocol.ReadAsync(
            new ProtocolContext(Candidate(), connection, definition),
            CancellationToken.None);

        var reading = Assert.Single(readings);
        Assert.Equal(83, reading.Battery.Percent);
        Assert.Equal(4010, reading.Battery.VoltageMv);
        Assert.Equal(ChargingState.Discharging, reading.Battery.Charging);

        Assert.Equal(2, connection.Writes.Count);
        Assert.Equal(0x11, connection.Writes[0][0]);
        Assert.Equal(0x00, connection.Writes[0][2]);
        Assert.Equal(0x0A, connection.Writes[0][3]);
        Assert.Equal(0x10, connection.Writes[0][4]);
        Assert.Equal(0x01, connection.Writes[0][5]);

        Assert.Equal(0x11, connection.Writes[1][0]);
        Assert.Equal(0x07, connection.Writes[1][2]);
        Assert.Equal(0x0A, connection.Writes[1][3]);
    }

    [Fact]
    public async Task FallsBackToBatteryStatusFeature()
    {
        var definition = LoadDefinition();
        var connection = new FakeHidConnection(Candidate());
        connection.Responder = request =>
        {
            if (IsFeatureQuery(request, 0x1001))
            {
                return LongResponse(0x00, 0x0A, 0x00);
            }

            if (IsFeatureQuery(request, 0x1000))
            {
                return LongResponse(0x00, 0x0A, 0x08);
            }

            if (request[2] == 0x08)
            {
                return LongResponse(0x08, 0x0A, 0x5F, 0x32, 0x00);
            }

            return null;
        };

        var protocol = new Hidpp2BatteryProtocol(
            NullLogger<Hidpp2BatteryProtocol>.Instance);
        var readings = await protocol.ReadAsync(
            new ProtocolContext(Candidate(), connection, definition),
            CancellationToken.None);

        var reading = Assert.Single(readings);
        Assert.Equal(95, reading.Battery.Percent);
    }

    [Fact]
    public async Task NoResponse_ThrowsProtocolException()
    {
        var definition = LoadDefinition(
            timeoutSeconds: 1,
            deviceIndexes: [1],
            batteryFeatures: ["0x1001"]);
        var connection = new FakeHidConnection(Candidate());

        var protocol = new Hidpp2BatteryProtocol(
            NullLogger<Hidpp2BatteryProtocol>.Instance);

        await Assert.ThrowsAsync<ProtocolException>(() =>
            protocol.ReadAsync(
                new ProtocolContext(Candidate(), connection, definition),
                CancellationToken.None));
    }

    private static bool IsFeatureQuery(byte[] request, int featureId) =>
        request[2] == 0x00
        && request[4] == (byte)(featureId >> 8)
        && request[5] == (byte)(featureId & 0xFF);

    private static byte[] LongResponse(
        byte featureIndex,
        byte functionByte,
        params byte[] parameters)
    {
        var response = new byte[20];
        response[0] = 0x11;
        response[1] = 0x01;
        response[2] = featureIndex;
        response[3] = functionByte;
        Array.Copy(parameters, 0, response, 4, parameters.Length);
        return response;
    }

    private static DeviceDefinition LoadDefinition(
        int? timeoutSeconds = null,
        int[]? deviceIndexes = null,
        string[]? batteryFeatures = null)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "g913-tkl.json");
        var json = File.ReadAllText(path);
        if (timeoutSeconds is not null
            || deviceIndexes is not null
            || batteryFeatures is not null)
        {
            var node = JsonNode.Parse(json)
                ?? throw new InvalidOperationException("Fixture produced an empty node.");
            if (timeoutSeconds is not null)
            {
                node["polling"]!["timeoutSeconds"] = timeoutSeconds.Value;
            }

            if (deviceIndexes is not null)
            {
                node["protocol"]!["options"]!["deviceIndexes"] =
                    new JsonArray(deviceIndexes.Select(index => (JsonNode)index).ToArray());
            }

            if (batteryFeatures is not null)
            {
                node["protocol"]!["options"]!["batteryFeatures"] =
                    new JsonArray(batteryFeatures.Select(feature => (JsonNode)feature).ToArray());
            }

            json = node.ToJsonString();
        }

        return JsonSerializer.Deserialize<DeviceDefinition>(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            }) ?? throw new InvalidOperationException("Fixture could not be deserialized.");
    }

    private static DeviceCandidate Candidate() => new(
        "g913-test",
        TransportKind.Hid,
        VendorId: 0x046D,
        ProductId: 0xC545,
        ProductName: "USB Receiver",
        UsagePage: 0xFF00,
        Usage: 0x0002,
        Interface: "MI_02",
        InputReportLength: 20,
        OutputReportLength: 20);

    private sealed class FakeHidConnection : IHidConnection
    {
        public FakeHidConnection(DeviceCandidate candidate)
        {
            Candidate = candidate;
        }

        public DeviceCandidate Candidate { get; }

        public bool IsOpen => true;

        public List<byte[]> Writes { get; } = [];

        public Func<byte[], byte[]?>? Responder { get; set; }

        private Queue<byte[]?> Responses { get; } = new();

        public Task WriteAsync(ReadOnlyMemory<byte> report, CancellationToken ct)
        {
            var request = report.ToArray();
            Writes.Add(request);
            Responses.Enqueue(Responder?.Invoke(request));
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                Responses.Count > 0 ? Responses.Dequeue() : null);
        }

        public Task<byte[]> GetFeatureAsync(
            byte reportId,
            int length,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task SetFeatureAsync(
            ReadOnlyMemory<byte> report,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
