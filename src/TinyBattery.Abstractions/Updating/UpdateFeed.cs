using TinyBattery.Abstractions.Updating;

namespace TinyBattery.Abstractions.Updating;

public sealed class UpdateFeed
{
    public int SchemaVersion { get; set; } = 1;

    public DateTimeOffset? GeneratedAt { get; set; }

    public List<UpdatePackageEntry> Packages { get; set; } = [];
}

public sealed class UpdatePackageEntry
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "0.0.0";

    public string? MinAppVersion { get; set; }

    public string Url { get; set; } = "";

    public string? Sha256 { get; set; }

    public long? Size { get; set; }

    public string? Notes { get; set; }

    public UpdateItemSet Items { get; set; } = new();
}

public sealed class UpdateItemSet
{
    public List<UpdateItemEntry> Devices { get; set; } = [];

    public List<UpdateItemEntry> Themes { get; set; } = [];
}

public sealed class UpdateItemEntry
{
    public string Id { get; set; } = "";

    public string Version { get; set; } = "0.0.0";
}

public sealed record UpdateItemChange(
    string Kind,
    string Id,
    string? CurrentVersion,
    string NewVersion);

public sealed record AvailableUpdate(
    UpdatePackageEntry Package,
    SemanticVersion CurrentVersion,
    bool IsCompatible,
    IReadOnlyList<UpdateItemChange> Changes);

public sealed record UpdateCheckResult(
    bool Success,
    string Message,
    IReadOnlyList<AvailableUpdate> Updates);

public sealed record UpdateInstallResult(
    bool Success,
    string Message,
    IReadOnlyList<string> InstalledPaths);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken ct);

    Task<UpdateInstallResult> InstallAsync(
        AvailableUpdate update,
        CancellationToken ct);
}
