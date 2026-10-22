using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Infrastructure.Paths;
using TinyBattery.Infrastructure.Settings;

namespace TinyBattery.Infrastructure.Tests;

public sealed class JsonUserSettingsStoreTests
{
    [Fact]
    public async Task MissingFile_ReturnsDefaults()
    {
        var root = CreateRoot();
        try
        {
            var store = CreateStore(root);

            await store.LoadAsync(CancellationToken.None);

            Assert.Equal(20, store.Current.LowBatteryThreshold);
            Assert.True(store.Current.NotifyOnLowBattery);
            Assert.Equal("classic", store.Current.BatteryThemeId);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var root = CreateRoot();
        try
        {
            var store = CreateStore(root);
            var settings = new UserSettings
            {
                LowBatteryThreshold = 15,
                PrimaryDeviceId = "lofree.flow-lite",
                StartWithWindows = true,
            };

            await store.SaveAsync(settings, CancellationToken.None);

            var reloaded = CreateStore(root);
            await reloaded.LoadAsync(CancellationToken.None);

            Assert.Equal(15, reloaded.Current.LowBatteryThreshold);
            Assert.Equal("lofree.flow-lite", reloaded.Current.PrimaryDeviceId);
            Assert.True(reloaded.Current.StartWithWindows);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static JsonUserSettingsStore CreateStore(string root) =>
        new(
            new AppPaths(root),
            NullLogger<JsonUserSettingsStore>.Instance);

    private static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "TinyBattery.Tests", Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
