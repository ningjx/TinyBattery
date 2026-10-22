using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Transports;
using TinyBattery.Protocols.Hid;
using TinyBattery.Protocols.Serialization;

namespace TinyBattery.Protocols.Tests;

public sealed class RequestResponseProtocolTests
{
    [Fact]
    public async Task LofreeStatusFrame_ReturnsPercentAndExpectedChecksum()
    {
        var definition = LoadLofree();
        var connection = new FakeHidConnection(
            Candidate(),
            LofreeResponse(0x5F));
        var protocol = new RequestResponseProtocol(
            NullLogger<RequestResponseProtocol>.Instance);

        var readings = await protocol.ReadAsync(
            new ProtocolContext(Candidate(), connection, definition),
            CancellationToken.None);

        var reading = Assert.Single(readings);
        Assert.Equal(95, reading.Battery.Percent);
        Assert.Equal(
            "08040000008000000000000000000000C9",
            Convert.ToHexString(Assert.Single(connection.Writes)));
    }

    [Fact]
    public async Task NoResponse_TimesOut()
    {
        var definition = LoadLofree(timeoutMs: 100);
        var connection = new FakeHidConnection(Candidate());
        var protocol = new RequestResponseProtocol(
            NullLogger<RequestResponseProtocol>.Instance);

        await Assert.ThrowsAsync<ProtocolException>(() =>
            protocol.ReadAsync(
                new ProtocolContext(Candidate(), connection, definition),
                CancellationToken.None));
    }

    [Fact]
    public async Task NonMatchingResponse_IsIgnored()
    {
        var definition = LoadLofree(timeoutMs: 100);
        var connection = new FakeHidConnection(
            Candidate(),
            LofreeResponse(0x5F, command: 0x03));
        var protocol = new RequestResponseProtocol(
            NullLogger<RequestResponseProtocol>.Instance);

        await Assert.ThrowsAsync<ProtocolException>(() =>
            protocol.ReadAsync(
                new ProtocolContext(Candidate(), connection, definition),
                CancellationToken.None));
    }

    [Fact]
    public async Task OutOfRangePercent_Throws()
    {
        var definition = LoadLofree();
        var connection = new FakeHidConnection(
            Candidate(),
            LofreeResponse(0xC8));
        var protocol = new RequestResponseProtocol(
            NullLogger<RequestResponseProtocol>.Instance);

        await Assert.ThrowsAsync<ProtocolException>(() =>
            protocol.ReadAsync(
                new ProtocolContext(Candidate(), connection, definition),
                CancellationToken.None));
    }

    private static DeviceDefinition LoadLofree(int timeoutMs = 2000)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lofree.flow-lite.json");
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("Fixture produced an empty JSON node.");
        node["protocol"]!["options"]!["timeoutMs"] = timeoutMs;

        return JsonSerializer.Deserialize<DeviceDefinition>(
            node.ToJsonString(),
            ProtocolJson.Options)
            ?? throw new InvalidOperationException("Fixture could not be deserialized.");
    }

    private static DeviceCandidate Candidate() => new(
        "lofree-test",
        TransportKind.Hid,
        VendorId: 0x05AC,
        ProductId: 0x024F,
        ProductName: "Lofree Flow Lite",
        UsagePage: 0xFF02,
        Interface: "MI_01",
        InputReportLength: 17,
        OutputReportLength: 17);

    private static byte[] LofreeResponse(byte percent, byte command = 0x04)
    {
        var response = new byte[17];
        response[0] = 0x08;
        response[1] = command;
        response[5] = 0x84;
        response[6] = percent;
        response[7] = 0x00;
        response[8] = 0x10;
        response[9] = 0xA5;
        return response;
    }

    private sealed class FakeHidConnection : IHidConnection
    {
        private readonly Queue<byte[]?> _responses;

        public FakeHidConnection(DeviceCandidate candidate, params byte[]?[] responses)
        {
            Candidate = candidate;
            _responses = new Queue<byte[]?>(responses);
        }

        public DeviceCandidate Candidate { get; }

        public bool IsOpen => true;

        public List<byte[]> Writes { get; } = [];

        public Task WriteAsync(ReadOnlyMemory<byte> report, CancellationToken ct)
        {
            Writes.Add(report.ToArray());
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                _responses.Count > 0 ? _responses.Dequeue() : null);
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
