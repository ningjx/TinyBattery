namespace TinyBattery.Protocols.Engine;

public enum ChecksumKind
{
    None,
    Sum8,
    Xor8,
    Crc8,
    Crc16,
    Crc32,
}
