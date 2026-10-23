using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Packages;
using TinyBattery.Abstractions.Paths;

namespace TinyBattery.Infrastructure.Packages;

public sealed class JsonInstalledPackageStore : IInstalledPackageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly IAppPaths _paths;
    private readonly ILogger<JsonInstalledPackageStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<InstalledPackage> _packages = [];

    public JsonInstalledPackageStore(
        IAppPaths paths,
        ILogger<JsonInstalledPackageStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstalledPackage>> LoadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_paths.InstalledPackagesFile))
            {
                _packages = [];
                return [];
            }

            var json = await File.ReadAllTextAsync(_paths.InstalledPackagesFile, ct);
            var index = JsonSerializer.Deserialize<InstalledPackageIndex>(json, JsonOptions);
            _packages = index?.Packages ?? [];
            return _packages;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Could not load installed package records from '{Path}'.",
                _paths.InstalledPackagesFile);
            _packages = [];
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordAsync(InstalledPackage package, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_packages.Count == 0 && File.Exists(_paths.InstalledPackagesFile))
            {
                var json = await File.ReadAllTextAsync(_paths.InstalledPackagesFile, ct);
                var existing = JsonSerializer.Deserialize<InstalledPackageIndex>(json, JsonOptions);
                _packages = existing?.Packages ?? [];
            }

            _packages.RemoveAll(candidate => string.Equals(
                candidate.Id,
                package.Id,
                StringComparison.OrdinalIgnoreCase));
            _packages.Add(package);
            await SaveAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string packageId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _packages.RemoveAll(candidate => string.Equals(
                candidate.Id,
                packageId,
                StringComparison.OrdinalIgnoreCase));
            await SaveAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_paths.InstalledPackagesFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            new InstalledPackageIndex { Packages = _packages },
            JsonOptions);
        var temporary = _paths.InstalledPackagesFile + ".tmp";
        await File.WriteAllTextAsync(temporary, json, ct);
        File.Move(temporary, _paths.InstalledPackagesFile, overwrite: true);
    }

    private sealed class InstalledPackageIndex
    {
        public List<InstalledPackage> Packages { get; set; } = [];
    }
}
