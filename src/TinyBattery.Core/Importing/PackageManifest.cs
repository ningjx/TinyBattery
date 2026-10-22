namespace TinyBattery.Core.Importing;

public sealed class PackageManifest
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public string? Author { get; init; }

    public string? Description { get; init; }

    public List<string>? Devices { get; init; }

    public List<string>? Themes { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}
