using TinyBattery.Abstractions.Settings;

namespace TinyBattery.Core.Tests;

internal sealed class FakeUserSettingsStore : IUserSettingsStore
{
    public UserSettings Current { get; set; } = new();

    public Task LoadAsync(CancellationToken ct) => Task.CompletedTask;

    public Task SaveAsync(UserSettings settings, CancellationToken ct)
    {
        Current = settings;
        return Task.CompletedTask;
    }
}
