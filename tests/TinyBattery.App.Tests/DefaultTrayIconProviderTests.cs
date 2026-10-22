using TinyBattery.App.Rendering;

namespace TinyBattery.App.Tests;

public sealed class DefaultTrayIconProviderTests
{
    [Fact]
    public void DefaultIcon_IsSizeAwareAndCached()
    {
        using var provider = new DefaultTrayIconProvider();

        var first = provider.GetDefaultIcon(32);
        var second = provider.GetDefaultIcon(32);
        var small = provider.GetDefaultIcon(16);

        Assert.Equal(32, first.Width);
        Assert.Equal(32, first.Height);
        Assert.Same(first, second);
        Assert.Equal(16, small.Width);
        Assert.Equal(16, small.Height);
    }
}
