namespace TinyBattery.Protocols.Engine;

public sealed class ChecksumSpec
{
    public ChecksumKind Kind { get; init; }

    public int[] Range { get; init; } = [0, 0];

    public int? Target { get; init; }

    public int At { get; init; }

    public uint? Polynomial { get; init; }

    public uint? Initial { get; init; }

    public uint? XorOut { get; init; }

    public bool ReflectInput { get; init; }

    public bool ReflectOutput { get; init; }

    public bool LittleEndian { get; init; }
}
