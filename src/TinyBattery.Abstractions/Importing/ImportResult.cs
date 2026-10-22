namespace TinyBattery.Abstractions.Importing;

public sealed record ImportResult(
    bool Success,
    IReadOnlyList<string> InstalledPaths,
    IReadOnlyList<string> Diagnostics)
{
    public static ImportResult Failure(params string[] diagnostics) =>
        new(false, [], diagnostics);

    public static ImportResult Ok(params string[] installedPaths) =>
        new(true, installedPaths, []);
}
