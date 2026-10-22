using TinyBattery.Abstractions.Protocols;

namespace TinyBattery.Protocols;

public sealed class ProtocolRegistry : IProtocolRegistry
{
    private readonly Dictionary<string, IBatteryProtocol> _protocols =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public ProtocolRegistry(IEnumerable<IBatteryProtocol> protocols)
    {
        foreach (var protocol in protocols)
        {
            Register(protocol);
        }
    }

    public void Register(IBatteryProtocol protocol)
    {
        lock (_gate)
        {
            _protocols[protocol.Kind] = protocol;
        }
    }

    public IBatteryProtocol Resolve(string kind)
    {
        if (TryResolve(kind, out var protocol))
        {
            return protocol!;
        }

        throw new ProtocolException(
            $"No protocol executor is registered for kind '{kind}'.");
    }

    public bool TryResolve(string kind, out IBatteryProtocol? protocol)
    {
        lock (_gate)
        {
            return _protocols.TryGetValue(kind, out protocol);
        }
    }
}
