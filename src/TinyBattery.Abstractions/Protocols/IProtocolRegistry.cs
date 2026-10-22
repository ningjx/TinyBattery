namespace TinyBattery.Abstractions.Protocols;

public interface IProtocolRegistry
{
    IBatteryProtocol Resolve(string kind);

    bool TryResolve(string kind, out IBatteryProtocol? protocol);

    void Register(IBatteryProtocol protocol);
}
