namespace TinyBattery.Abstractions.Importing;

public sealed record ImportOptions(
    bool Overwrite = false,
    bool AllowPlugins = false,
    string? Source = null);
