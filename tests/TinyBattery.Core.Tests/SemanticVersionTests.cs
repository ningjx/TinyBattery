using TinyBattery.Abstractions.Updating;

namespace TinyBattery.Core.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.2.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    public void ComparesBySemanticVersion(string left, string right, int expectedSign)
    {
        var result = SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));

        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.x")]
    public void RejectsInvalidVersions(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }
}
