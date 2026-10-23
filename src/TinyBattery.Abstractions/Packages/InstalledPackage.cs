namespace TinyBattery.Abstractions.Packages;

public sealed class InstalledPackage
{
    public string Id { get; set; } = "";

    public string Version { get; set; } = "0.0.0";

    public string? Source { get; set; }

    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;

    public List<InstalledPackageItem> Items { get; set; } = [];
}

public sealed class InstalledPackageItem
{
    public string Kind { get; set; } = "";

    public string Id { get; set; } = "";

    public string? Version { get; set; }

    public string? Path { get; set; }
}

public interface IInstalledPackageStore
{
    Task<IReadOnlyList<InstalledPackage>> LoadAsync(CancellationToken ct);

    Task RecordAsync(InstalledPackage package, CancellationToken ct);

    Task RemoveAsync(string packageId, CancellationToken ct);
}
