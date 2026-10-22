using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Themes;

namespace TinyBattery.Core.Importing;

public sealed class IconThemeImporter : IIconThemeImporter
{
    private static readonly HashSet<string> ThemeExtensions = new(
        [".json", ".png", ".ico", ".jpg", ".jpeg", ".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IAppPaths _paths;
    private readonly IconThemeLoader _loader;
    private readonly SafeZipExtractor _zip;
    private readonly ILogger<IconThemeImporter> _logger;

    public IconThemeImporter(
        IAppPaths paths,
        IconThemeLoader loader,
        SafeZipExtractor zip,
        ILogger<IconThemeImporter> logger)
    {
        _paths = paths;
        _loader = loader;
        _zip = zip;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        string sourcePath,
        ImportOptions options,
        CancellationToken ct)
    {
        string? temporaryDirectory = null;
        try
        {
            var prepared = await PrepareSourceAsync(sourcePath, ct);
            temporaryDirectory = prepared.TemporaryDirectory;
            var manifestPath = prepared.ManifestPath;
            var singleJson = prepared.SingleJson;

            if (!_loader.TryLoad(manifestPath, out var theme, out var errors)
                || theme is null)
            {
                return new ImportResult(false, [], errors);
            }

            if (string.Equals(theme.Id, "classic", StringComparison.OrdinalIgnoreCase))
            {
                return ImportResult.Failure(
                    "Theme id 'classic' is reserved for the built-in theme.");
            }

            if (singleJson && theme.Kind != IconThemeKind.Classic)
            {
                return ImportResult.Failure(
                    "Image-set and plugin themes must be imported as a folder or .tbtheme/.zip archive.");
            }

            var destination = Path.Combine(
                _paths.ThemesDirectory,
                theme.Id);

            if (Directory.Exists(destination) && !options.Overwrite)
            {
                return ImportResult.Failure(
                    $"Icon theme '{theme.Id}' already exists. Use overwrite to replace it.");
            }

            Directory.CreateDirectory(_paths.ThemesDirectory);
            var staging = destination + ".new-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);

            var sourceDirectory = Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidOperationException("Theme manifest has no directory.");
            CopyThemeFiles(sourceDirectory, staging, singleJson, manifestPath, theme);

            Install(staging, destination, options.Overwrite);

            _logger.LogInformation(
                "Imported icon theme '{Id}' to '{Path}'.",
                theme.Id,
                destination);

            return ImportResult.Ok(destination);
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

    private async Task<PreparedSource> PrepareSourceAsync(
        string sourcePath,
        CancellationToken ct)
    {
        if (Directory.Exists(sourcePath))
        {
            var manifest = Path.Combine(sourcePath, "theme.json");
            if (!File.Exists(manifest))
            {
                throw new InvalidDataException(
                    $"Theme folder '{sourcePath}' does not contain theme.json.");
            }

            return new PreparedSource(manifest, false, null);
        }

        if (!File.Exists(sourcePath))
        {
            throw new IOException($"Source not found: {sourcePath}");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension == ".json")
        {
            return new PreparedSource(sourcePath, true, null);
        }

        if (extension is ".zip" or ".tbtheme")
        {
            var temporary = Path.Combine(
                Path.GetTempPath(),
                "TinyBattery.Import",
                Guid.NewGuid().ToString("N"));
            await _zip.ExtractAsync(
                sourcePath,
                temporary,
                ThemeExtensions,
                ct);

            var manifest = Directory
                .EnumerateFiles(temporary, "theme.json", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();
            if (manifest is null)
            {
                throw new InvalidDataException(
                    "The theme archive does not contain theme.json.");
            }

            return new PreparedSource(manifest, false, temporary);
        }

        throw new InvalidDataException(
            "Icon themes must be imported as a folder, .json, .tbtheme, or .zip.");
    }

    private static void CopyThemeFiles(
        string sourceDirectory,
        string destination,
        bool singleJson,
        string manifestPath,
        IconThemeDescriptor theme)
    {
        if (singleJson)
        {
            File.Copy(manifestPath, Path.Combine(destination, "theme.json"));
            CopyAsset(theme.PreviewPath, sourceDirectory, destination);
            CopyAsset(theme.AppIconPath, sourceDirectory, destination);
            CopyAsset(theme.UnknownAssetPath, sourceDirectory, destination);
            CopyAsset(theme.ChargingOverlayPath, sourceDirectory, destination);
            foreach (var level in theme.Levels ?? [])
            {
                CopyAsset(level.AssetPath, sourceDirectory, destination);
            }

            return;
        }

        var sourceFull = Path.GetFullPath(sourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var destinationFull = Path.GetFullPath(destination);
        Directory.CreateDirectory(destinationFull);

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException(
                    $"Theme file '{file}' is a reparse point.");
            }

            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (!ThemeExtensions.Contains(extension))
            {
                continue;
            }

            var full = Path.GetFullPath(file);
            if (!full.StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Theme file '{file}' escapes its source directory.");
            }

            var relative = Path.GetRelativePath(sourceDirectory, full);
            var target = Path.GetFullPath(Path.Combine(destinationFull, relative));
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(full, target, overwrite: true);
        }
    }

    private static void CopyAsset(
        string? assetPath,
        string sourceDirectory,
        string destination)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
        {
            return;
        }

        var relative = Path.GetRelativePath(sourceDirectory, assetPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Theme asset '{assetPath}' escapes its source directory.");
        }

        var target = Path.GetFullPath(Path.Combine(destination, relative));
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(assetPath, target, overwrite: true);
    }

    private static void Install(string staging, string destination, bool overwrite)
    {
        string? backup = null;
        if (Directory.Exists(destination))
        {
            if (!overwrite)
            {
                throw new IOException(
                    $"Icon theme already exists at '{destination}'.");
            }

            backup = destination + ".old-" + Guid.NewGuid().ToString("N");
            Directory.Move(destination, backup);
        }

        try
        {
            Directory.Move(staging, destination);
        }
        catch
        {
            if (backup is not null && Directory.Exists(backup))
            {
                Directory.Move(backup, destination);
            }

            throw;
        }

        if (backup is not null && Directory.Exists(backup))
        {
            Directory.Delete(backup, recursive: true);
        }
    }

    private sealed record PreparedSource(
        string ManifestPath,
        bool SingleJson,
        string? TemporaryDirectory);
}
