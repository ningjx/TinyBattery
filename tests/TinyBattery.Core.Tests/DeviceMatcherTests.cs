using System.Text.Json;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Core.Matching;
using Microsoft.Extensions.Logging.Abstractions;

namespace TinyBattery.Core.Tests;

public sealed class DeviceMatcherTests
{
    [Fact]
    public void ExactHidMatch_Succeeds()
    {
        var definition = Definition(
            "lofree.test",
            new DeviceMatch(
                TransportKind.Hid,
                VendorId: 0x05AC,
                ProductId: 0x024F,
                UsagePage: 0xFF02,
                InputReportLength: 17,
                OutputReportLength: 17));
        var matcher = CreateMatcher(definition);
        var candidate = Candidate();

        var result = matcher.Match(candidate);

        Assert.NotNull(result);
        Assert.Equal("lofree.test", result!.Definition.Id);
        Assert.True(result.Score > 0);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void WrongVendor_DoesNotMatch()
    {
        var definition = Definition(
            "lofree.test",
            new DeviceMatch(TransportKind.Hid, VendorId: 0x1234));
        var matcher = CreateMatcher(definition);

        var result = matcher.Match(Candidate());

        Assert.Null(result);
    }

    [Fact]
    public void MoreSpecificDefinition_Wins()
    {
        var generic = Definition(
            "generic",
            new DeviceMatch(TransportKind.Hid, VendorId: 0x05AC));
        var specific = Definition(
            "specific",
            new DeviceMatch(
                TransportKind.Hid,
                VendorId: 0x05AC,
                ProductId: 0x024F,
                UsagePage: 0xFF02));
        var matcher = CreateMatcher(generic, specific);

        var result = matcher.Match(Candidate());

        Assert.NotNull(result);
        Assert.Equal("specific", result!.Definition.Id);
    }

    [Fact]
    public void HigherPriority_BreaksTie()
    {
        var low = Definition(
            "low",
            new DeviceMatch(TransportKind.Hid, VendorId: 0x05AC),
            priority: 0);
        var high = Definition(
            "high",
            new DeviceMatch(TransportKind.Hid, VendorId: 0x05AC),
            priority: 100);
        var matcher = CreateMatcher(low, high);

        var result = matcher.Match(Candidate());

        Assert.NotNull(result);
        Assert.Equal("high", result!.Definition.Id);
    }

    private static DeviceMatcher CreateMatcher(params DeviceDefinition[] definitions) =>
        new(new StubStore(definitions), NullLogger<DeviceMatcher>.Instance);

    private static DeviceDefinition Definition(
        string id,
        DeviceMatch match,
        int priority = 0)
    {
        using var options = JsonDocument.Parse(
            """
            {
              "reportId": 0,
              "includeReportId": true,
              "timeoutMs": 1000,
              "request": { "payload": [0] },
              "response": { "match": [{ "offset": 0, "equals": 0 }] },
              "extract": { "percent": { "offset": 0, "type": "u8" } }
            }
            """);

        return new DeviceDefinition(
            1,
            id,
            id,
            null,
            match,
            new ProtocolDefinition("hid.request-response", options.RootElement.Clone()),
            new PollingOptions(),
            Priority: priority);
    }

    private static DeviceCandidate Candidate() => new(
        "test-device",
        TransportKind.Hid,
        VendorId: 0x05AC,
        ProductId: 0x024F,
        ProductName: "Lofree Flow Lite",
        UsagePage: 0xFF02,
        Interface: "MI_01",
        InputReportLength: 17,
        OutputReportLength: 17);

    private sealed class StubStore : IDeviceDefinitionStore
    {
        public StubStore(params DeviceDefinition[] definitions)
        {
            Definitions = definitions;
        }

        public IReadOnlyList<DeviceDefinition> Definitions { get; }

        public IReadOnlyList<DefinitionDiagnostic> Diagnostics => [];

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
