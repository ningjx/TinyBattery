using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Protocols;

namespace TinyBattery.Core.Tests;

internal sealed class FakeProtocolRegistry : IProtocolRegistry
{
    private readonly HashSet<string> _kinds;

    public FakeProtocolRegistry(params string[] kinds)
    {
        _kinds = new HashSet<string>(kinds, StringComparer.OrdinalIgnoreCase);
    }

    public IBatteryProtocol Resolve(string kind) =>
        TryResolve(kind, out var protocol)
            ? protocol!
            : throw new ProtocolException($"No protocol for '{kind}'.");

    public bool TryResolve(string kind, out IBatteryProtocol? protocol)
    {
        if (_kinds.Contains(kind))
        {
            protocol = DummyProtocol.Instance;
            return true;
        }

        protocol = null;
        return false;
    }

    public void Register(IBatteryProtocol protocol) => _kinds.Add(protocol.Kind);

    private sealed class DummyProtocol : IBatteryProtocol
    {
        public static readonly DummyProtocol Instance = new();

        public string Kind => "dummy";

        public Task<IReadOnlyList<ProtocolReading>> ReadAsync(
            ProtocolContext context,
            CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
