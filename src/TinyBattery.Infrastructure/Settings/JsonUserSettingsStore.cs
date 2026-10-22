using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Settings;

namespace TinyBattery.Infrastructure.Settings;

public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly IAppPaths _paths;
    private readonly ILogger<JsonUserSettingsStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private UserSettings _current = new();

    public JsonUserSettingsStore(
        IAppPaths paths,
        ILogger<JsonUserSettingsStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public UserSettings Current => _current;

    public async Task LoadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                _current = new UserSettings();
                return;
            }

            var json = await File.ReadAllTextAsync(_paths.SettingsFile, ct);
            _current = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
                ?? new UserSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Could not load user settings from '{Path}'; using defaults.",
                _paths.SettingsFile);
            _current = new UserSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(_paths.SettingsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var temporaryPath = _paths.SettingsFile + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, ct);
            File.Move(temporaryPath, _paths.SettingsFile, overwrite: true);
            _current = settings;
        }
        finally
        {
            _gate.Release();
        }
    }
}
