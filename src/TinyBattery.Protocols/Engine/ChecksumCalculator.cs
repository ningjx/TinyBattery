using TinyBattery.Abstractions.Protocols;

namespace TinyBattery.Protocols.Engine;

public static class ChecksumCalculator
{
    public static void ValidateFrameLength(int frameLength, ChecksumSpec spec)
    {
        if (spec.Kind == ChecksumKind.None)
        {
            return;
        }

        if (spec.Range.Length != 2)
        {
            throw new ProtocolException("Checksum range must contain exactly two values.");
        }

        var start = spec.Range[0];
        var end = spec.Range[1];
        if (start < 0 || end < start || end >= frameLength)
        {
            throw new ProtocolException(
                $"Checksum range [{start}, {end}] is outside a {frameLength}-byte frame.");
        }

        var width = GetWidth(spec.Kind);
        if (spec.At < 0 || spec.At + width > frameLength)
        {
            throw new ProtocolException(
                $"Checksum offset {spec.At} with width {width} is outside a {frameLength}-byte frame.");
        }

        if (spec.Kind == ChecksumKind.Sum8 && spec.Target is null)
        {
            throw new ProtocolException("sum8 checksums require a target value.");
        }
    }

    public static void Apply(Span<byte> frame, ChecksumSpec spec)
    {
        ValidateFrameLength(frame.Length, spec);

        if (spec.Kind == ChecksumKind.None)
        {
            return;
        }

        var value = Compute(frame, spec);
        var width = GetWidth(spec.Kind);
        for (var index = 0; index < width; index++)
        {
            var shift = spec.LittleEndian ? index * 8 : (width - 1 - index) * 8;
            frame[spec.At + index] = (byte)(value >> shift);
        }
    }

    public static uint Compute(ReadOnlySpan<byte> frame, ChecksumSpec spec)
    {
        ValidateFrameLength(frame.Length, spec);

        var start = spec.Range[0];
        var end = spec.Range[1];
        var data = frame[start..(end + 1)];

        return spec.Kind switch
        {
            ChecksumKind.None => 0,
            ChecksumKind.Sum8 => Sum8(data, spec.Target!.Value),
            ChecksumKind.Xor8 => Xor8(data),
            ChecksumKind.Crc8 => Crc(data, 8, spec, 0x07, 0x00),
            ChecksumKind.Crc16 => Crc(data, 16, spec, 0x1021, 0xFFFF),
            ChecksumKind.Crc32 => Crc(data, 32, spec, 0x04C11DB7, 0xFFFFFFFF),
            _ => throw new ProtocolException($"Unsupported checksum kind {spec.Kind}."),
        };
    }

    private static int GetWidth(ChecksumKind kind) => kind switch
    {
        ChecksumKind.None => 0,
        ChecksumKind.Sum8 or ChecksumKind.Xor8 or ChecksumKind.Crc8 => 1,
        ChecksumKind.Crc16 => 2,
        ChecksumKind.Crc32 => 4,
        _ => throw new ProtocolException($"Unsupported checksum kind {kind}."),
    };

    private static uint Sum8(ReadOnlySpan<byte> data, int target)
    {
        var sum = 0u;
        foreach (var value in data)
        {
            sum = (sum + value) & 0xFF;
        }

        return ((uint)target - sum) & 0xFF;
    }

    private static uint Xor8(ReadOnlySpan<byte> data)
    {
        var value = 0u;
        foreach (var item in data)
        {
            value ^= item;
        }

        return value & 0xFF;
    }

    private static uint Crc(
        ReadOnlySpan<byte> data,
        int width,
        ChecksumSpec spec,
        uint defaultPolynomial,
        uint defaultInitial)
    {
        var polynomial = spec.Polynomial ?? defaultPolynomial;
        var initial = spec.Initial ?? defaultInitial;
        var xorOut = spec.XorOut ?? 0;
        var topBit = 1u << (width - 1);
        var mask = width == 32 ? uint.MaxValue : (1u << width) - 1;

        var crc = initial & mask;
        foreach (var item in data)
        {
            var value = spec.ReflectInput ? Reflect(item, 8) : item;
            crc ^= (uint)value << (width - 8);

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & topBit) != 0
                    ? ((crc << 1) ^ polynomial) & mask
                    : (crc << 1) & mask;
            }
        }

        if (spec.ReflectOutput)
        {
            crc = Reflect(crc, width);
        }

        return (crc ^ xorOut) & mask;
    }

    private static uint Reflect(uint value, int width)
    {
        var result = 0u;
        for (var bit = 0; bit < width; bit++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }

        return result;
    }
}
