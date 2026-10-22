using System.Text;
using TinyBattery.Protocols.Engine;

namespace TinyBattery.Protocols.Tests;

public sealed class ChecksumTests
{
    [Fact]
    public void LofreeSum8_ProducesExpectedByte()
    {
        var frame = new byte[17];
        frame[0] = 0x08;
        frame[1] = 0x04;
        frame[5] = 0x80;

        var spec = new ChecksumSpec
        {
            Kind = ChecksumKind.Sum8,
            Range = [0, 15],
            Target = 0x55,
            At = 16,
        };

        ChecksumCalculator.Apply(frame, spec);

        Assert.Equal(0xC9, frame[16]);
        Assert.Equal(0x55, Sum(frame));
    }

    [Fact]
    public void Xor8_ComputesXorOfRange()
    {
        var frame = new byte[] { 0x01, 0x02, 0x04, 0x00 };
        var spec = new ChecksumSpec
        {
            Kind = ChecksumKind.Xor8,
            Range = [0, 2],
            At = 3,
        };

        ChecksumCalculator.Apply(frame, spec);

        Assert.Equal(0x07, frame[3]);
    }

    [Fact]
    public void Crc16_CcittFalse_MatchesKnownVector()
    {
        var payload = Encoding.ASCII.GetBytes("123456789");
        var frame = new byte[payload.Length + 2];
        payload.CopyTo(frame, 0);

        var spec = new ChecksumSpec
        {
            Kind = ChecksumKind.Crc16,
            Range = [0, payload.Length - 1],
            At = payload.Length,
            Polynomial = 0x1021,
            Initial = 0xFFFF,
        };

        ChecksumCalculator.Apply(frame, spec);

        Assert.Equal(0x29, frame[^2]);
        Assert.Equal(0xB1, frame[^1]);
    }

    [Fact]
    public void ChecksumOutsideFrame_Throws()
    {
        var frame = new byte[4];
        var spec = new ChecksumSpec
        {
            Kind = ChecksumKind.Sum8,
            Range = [0, 10],
            Target = 0,
            At = 3,
        };

        Assert.Throws<TinyBattery.Abstractions.Protocols.ProtocolException>(
            () => ChecksumCalculator.Apply(frame, spec));
    }

    private static int Sum(byte[] frame)
    {
        var sum = 0;
        foreach (var value in frame)
        {
            sum += value;
        }

        return sum & 0xFF;
    }
}
