using TinyBattery.Abstractions.Protocols;
using TinyBattery.Protocols.Engine;

namespace TinyBattery.Protocols.Tests;

public sealed class FieldExtractorTests
{
    [Fact]
    public void U8_ReadsExpectedValue()
    {
        var data = new byte[] { 0, 0, 0, 0, 0, 0, 95 };

        var value = FieldExtractor.Extract(
            data,
            new FieldSpec { Offset = 6, Type = "u8" });

        Assert.Equal(95, value);
    }

    [Fact]
    public void U16Be_ReadsExpectedValue()
    {
        var data = new byte[] { 0x12, 0x34 };

        var value = FieldExtractor.Extract(
            data,
            new FieldSpec { Offset = 0, Type = "u16be" });

        Assert.Equal(0x1234, value);
    }

    [Fact]
    public void I8_PreservesSign()
    {
        var data = new byte[] { 0xFF };

        var value = FieldExtractor.Extract(
            data,
            new FieldSpec { Offset = 0, Type = "i8" });

        Assert.Equal(-1, value);
    }

    [Fact]
    public void Bits_ExtractsSubByteField()
    {
        var data = new byte[] { 0b1010_1100 };

        var value = FieldExtractor.Extract(
            data,
            new FieldSpec
            {
                Offset = 0,
                Type = "bits",
                BitOffset = 2,
                BitCount = 3,
            });

        Assert.Equal(3, value);
    }

    [Fact]
    public void OutOfRange_Throws()
    {
        var data = new byte[] { 200 };

        Assert.Throws<ProtocolException>(() =>
            FieldExtractor.Extract(
                data,
                new FieldSpec { Offset = 0, Type = "u8", Max = 100 }));
    }
}
