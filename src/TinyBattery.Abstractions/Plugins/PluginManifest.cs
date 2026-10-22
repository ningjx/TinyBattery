namespace TinyBattery.Abstractions.Plugins;

public sealed class PluginManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public string? Description { get; init; }

    public string? ApiVersion { get; init; }

    public string? Assembly { get; init; }

    public string? EntryType { get; init; }

    public List<string>? Devices { get; init; }

    public List<string>? Themes { get; init; }
}
