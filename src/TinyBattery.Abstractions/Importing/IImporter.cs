namespace TinyBattery.Abstractions.Importing;

public interface IImporter
{
    Task<ImportResult> ImportAsync(
        string sourcePath,
        ImportOptions options,
        CancellationToken ct);
}
