namespace TinyBattery.Protocols.Engine;

public sealed class FieldSpec
{
    public int Offset { get; init; }

    public string Type { get; init; } = "u8";

    public double? Min { get; init; }

    public double? Max { get; init; }

    public ulong? Mask { get; init; }

    public int Shift { get; init; }

    public int? BitOffset { get; init; }

    public int? BitCount { get; init; }

    public Dictionary<string, string>? Map { get; init; }
}
