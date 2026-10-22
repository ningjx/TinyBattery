using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Core.Serialization;

namespace TinyBattery.Core.Importing;

public sealed class PackageImporter : IPackageImporter
{
    private static readonly HashSet<string> PackageExtensions = new(
        [".json", ".png", ".ico", ".jpg", ".jpeg", ".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IDeviceDefinitionImporter _devices;
    private readonly IIconThemeImporter _themes;
    private readonly PackageValidator _validator;
    private readonly SafeZipExtractor _zip;
    private readonly ILogger<PackageImporter> _logger;

    public PackageImporter(
        IDeviceDefinitionImporter devices,
        IIconThemeImporter themes,
        PackageValidator validator,
        SafeZipExtractor zip,
        ILogger<PackageImporter> logger)
    {
        _devices = devices;
        _themes = themes;
        _validator = validator;
        _zip = zip;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        string sourcePath,
        ImportOptions options,
        CancellationToken ct)
    {
        string? temporaryDirectory = null;
        string? explicitManifest = null;
        try
        {
            var root = sourcePath;
            if (File.Exists(sourcePath))
            {
                var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                if (extension == ".json")
                {
                    explicitManifest = sourcePath;
                    root = Path.GetDirectoryName(sourcePath)
                        ?? throw new InvalidOperationException(
                            "Package manifest has no directory.");
                }
                else if (extension is ".zip" or ".tbpack")
                {
                    temporaryDirectory = Path.Combine(
                        Path.GetTempPath(),
                        "TinyBattery.Import",
                        Guid.NewGuid().ToString("N"));
                    await _zip.ExtractAsync(
                        sourcePath,
                        temporaryDirectory,
                        PackageExtensions,
                        ct);
                    root = temporaryDirectory;
                }
                else
                {
                    return ImportResult.Failure(
                        "Packages must be a folder, package.json, .zip, or .tbpack.");
                }
            }
            else if (!Directory.Exists(sourcePath))
            {
                return ImportResult.Failure($"Source not found: {sourcePath}");
            }

            var manifestPath = explicitManifest
                ?? Directory
                    .EnumerateFiles(root, "package.json", SearchOption.AllDirectories)
                    .OrderBy(path => path.Length)
                    .FirstOrDefault();
            if (manifestPath is null)
            {
                return ImportResult.Failure(
                    "The package does not contain package.json.");
            }

            string json;
            try
            {
                json = await File.ReadAllTextAsync(manifestPath, ct);
            }
            catch (IOException exception)
            {
                return ImportResult.Failure(exception.Message);
            }

            var validation = _validator.Validate(json);
            if (!validation.IsValid)
            {
                return new ImportResult(false, [], validation.Errors);
            }

            PackageManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PackageManifest>(
                    json,
                    DefinitionJson.Options);
            }
            catch (JsonException exception)
            {
                return ImportResult.Failure(exception.Message);
            }

            if (manifest is null)
            {
                return ImportResult.Failure(
                    "The package manifest produced an empty result.");
            }

            var packageRoot = Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidOperationException("Package manifest has no directory.");
            var installed = new List<string>();
            var diagnostics = new List<string>();

            foreach (var device in manifest.Devices ?? [])
            {
                ct.ThrowIfCancellationRequested();
                var path = ResolveEntry(packageRoot, device, diagnostics);
                if (path is null)
                {
                    continue;
                }

                var result = await _devices.ImportAsync(path, options, ct);
                installed.AddRange(result.InstalledPaths);
                diagnostics.AddRange(result.Diagnostics);
            }

            foreach (var theme in manifest.Themes ?? [])
            {
                ct.ThrowIfCancellationRequested();
                var path = ResolveEntry(packageRoot, theme, diagnostics);
                if (path is null)
                {
                    continue;
                }

                if (File.Exists(path)
                    && string.Equals(
                        Path.GetFileName(path),
                        "theme.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.GetDirectoryName(path) ?? path;
                }

                var result = await _themes.ImportAsync(path, options, ct);
                installed.AddRange(result.InstalledPaths);
                diagnostics.AddRange(result.Diagnostics);
            }

            if (installed.Count == 0 && diagnostics.Count == 0)
            {
                diagnostics.Add("The package contains no devices or themes.");
            }

            _logger.LogInformation(
                "Imported package '{Id}': {Count} item(s).",
                manifest.Id,
                installed.Count);

            return new ImportResult(installed.Count > 0, installed, diagnostics);
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException)
        {
            return ImportResult.Failure(exception.Message);
        }
        finally
        {
            if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    private static string? ResolveEntry(
        string packageRoot,
        string relativePath,
        List<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            diagnostics.Add("Package entry path is empty.");
            return null;
        }

        var rootFull = Path.GetFullPath(packageRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(packageRoot, relativePath));
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add($"Package entry '{relativePath}' escapes the package.");
            return null;
        }

        if (!File.Exists(full) && !Directory.Exists(full))
        {
            diagnostics.Add($"Package entry '{relativePath}' was not found.");
            return null;
        }

        return full;
    }
}
