using TinyBattery.Abstractions.Packages;

namespace TinyBattery.Core.Tests;

internal sealed class FakeInstalledPackageStore : IInstalledPackageStore
{
    private readonly List<InstalledPackage> _packages = [];

    public Task<IReadOnlyList<InstalledPackage>> LoadAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstalledPackage>>(_packages.ToArray());

    public Task RecordAsync(InstalledPackage package, CancellationToken ct)
    {
        _packages.RemoveAll(candidate => string.Equals(
            candidate.Id,
            package.Id,
            StringComparison.OrdinalIgnoreCase));
        _packages.Add(package);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string packageId, CancellationToken ct)
    {
        _packages.RemoveAll(candidate => string.Equals(
            candidate.Id,
            packageId,
            StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }
}
