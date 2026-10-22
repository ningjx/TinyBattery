using System.Buffers.Binary;
using System.Globalization;
using TinyBattery.Abstractions.Protocols;

namespace TinyBattery.Protocols.Engine;

public static class FieldExtractor
{
    public static double Extract(ReadOnlySpan<byte> data, FieldSpec spec)
    {
        var type = spec.Type.ToLowerInvariant();
        double value = type switch
        {
            "u8" => Read(data, spec.Offset, 1),
            "i8" => (sbyte)Read(data, spec.Offset, 1),
            "u16le" => ReadUInt16(data, spec.Offset, littleEndian: true),
            "u16be" => ReadUInt16(data, spec.Offset, littleEndian: false),
            "i16le" => (short)ReadUInt16(data, spec.Offset, littleEndian: true),
            "i16be" => (short)ReadUInt16(data, spec.Offset, littleEndian: false),
            "u24le" => ReadUInt24(data, spec.Offset, littleEndian: true),
            "u24be" => ReadUInt24(data, spec.Offset, littleEndian: false),
            "u32le" => ReadUInt32(data, spec.Offset, littleEndian: true),
            "u32be" => ReadUInt32(data, spec.Offset, littleEndian: false),
            "i32le" => (int)ReadUInt32(data, spec.Offset, littleEndian: true),
            "i32be" => (int)ReadUInt32(data, spec.Offset, littleEndian: false),
            "bits" => ReadBits(data, spec),
            _ => throw new ProtocolException($"Unsupported field type '{spec.Type}'."),
        };

        if (spec.Mask is not null || spec.Shift != 0)
        {
            var raw = (ulong)value;
            if (spec.Mask is not null)
            {
                raw &= spec.Mask.Value;
            }

            if (spec.Shift > 0)
            {
                raw >>= spec.Shift;
            }
            else if (spec.Shift < 0)
            {
                raw <<= -spec.Shift;
            }

            value = raw;
        }

        if (spec.Min is not null && value < spec.Min.Value)
        {
            throw new ProtocolException(
                $"Field value {value.ToString(CultureInfo.InvariantCulture)} is below {spec.Min}.");
        }

        if (spec.Max is not null && value > spec.Max.Value)
        {
            throw new ProtocolException(
                $"Field value {value.ToString(CultureInfo.InvariantCulture)} is above {spec.Max}.");
        }

        return value;
    }

    private static ulong Read(ReadOnlySpan<byte> data, int offset, int length)
    {
        EnsureLength(data, offset, length);
        return length switch
        {
            1 => data[offset],
            _ => throw new ProtocolException($"Unsupported read length {length}."),
        };
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> data,
        int offset,
        bool littleEndian)
    {
        EnsureLength(data, offset, 2);
        return littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
    }

    private static uint ReadUInt24(
        ReadOnlySpan<byte> data,
        int offset,
        bool littleEndian)
    {
        EnsureLength(data, offset, 3);
        return littleEndian
            ? (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16))
            : (uint)((data[offset] << 16)
                | (data[offset + 1] << 8)
                | data[offset + 2]);
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> data,
        int offset,
        bool littleEndian)
    {
        EnsureLength(data, offset, 4);
        return littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
    }

    private static ulong ReadBits(ReadOnlySpan<byte> data, FieldSpec spec)
    {
        if (spec.BitOffset is null || spec.BitCount is null)
        {
            throw new ProtocolException("Bit fields require bitOffset and bitCount.");
        }

        var bitOffset = spec.BitOffset.Value;
        var bitCount = spec.BitCount.Value;
        if (bitOffset is < 0 or > 7)
        {
            throw new ProtocolException("bitOffset must be between 0 and 7.");
        }

        if (bitCount is < 1 or > 32)
        {
            throw new ProtocolException("bitCount must be between 1 and 32.");
        }

        var byteCount = (bitOffset + bitCount + 7) / 8;
        EnsureLength(data, spec.Offset, byteCount);

        ulong value = 0;
        for (var index = 0; index < byteCount; index++)
        {
            value |= (ulong)data[spec.Offset + index] << (index * 8);
        }

        value >>= bitOffset;
        var mask = bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1;
        return value & mask;
    }

    private static void EnsureLength(ReadOnlySpan<byte> data, int offset, int length)
    {
        if (offset < 0 || length < 1 || offset + length > data.Length)
        {
            throw new ProtocolException(
                $"Field range [{offset}, {offset + length}) is outside a {data.Length}-byte payload.");
        }
    }
}
