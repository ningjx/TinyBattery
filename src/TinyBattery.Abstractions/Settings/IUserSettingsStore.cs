namespace TinyBattery.Abstractions.Settings;

public interface IUserSettingsStore
{
    UserSettings Current { get; }

    Task LoadAsync(CancellationToken ct);

    Task SaveAsync(UserSettings settings, CancellationToken ct);
}
