using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Packages;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Abstractions.Updating;
using TinyBattery.Core.Serialization;
using TinyBattery.Core.Themes;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Importing;

public sealed class PackageImporter : IPackageImporter
{
    private static readonly HashSet<string> PackageExtensions = new(
        [".json", ".svg", ".png", ".ico", ".jpg", ".jpeg", ".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IDeviceDefinitionImporter _devices;
    private readonly IIconThemeImporter _themes;
    private readonly PackageValidator _validator;
    private readonly SafeZipExtractor _zip;
    private readonly DefinitionValidator _definitionValidator;
    private readonly IProtocolRegistry _protocols;
    private readonly IconThemeLoader _themeLoader;
    private readonly IInstalledPackageStore _installedPackages;
    private readonly IAppPaths _paths;
    private readonly ILogger<PackageImporter> _logger;

    public PackageImporter(
        IDeviceDefinitionImporter devices,
        IIconThemeImporter themes,
        PackageValidator validator,
        SafeZipExtractor zip,
        DefinitionValidator definitionValidator,
        IProtocolRegistry protocols,
        IconThemeLoader themeLoader,
        IInstalledPackageStore installedPackages,
        IAppPaths paths,
        ILogger<PackageImporter> logger)
    {
        _devices = devices;
        _themes = themes;
        _validator = validator;
        _zip = zip;
        _definitionValidator = definitionValidator;
        _protocols = protocols;
        _themeLoader = themeLoader;
        _installedPackages = installedPackages;
        _paths = paths;
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
            var root = sourcePath;
            string? explicitManifest = null;
            if (File.Exists(sourcePath))
            {
                var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                if (extension == ".json")
                {
                    return await ImportSingleJsonAsync(sourcePath, options, ct);
                }

                if (extension is ".zip" or ".tbpack" or ".tbtheme")
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
                        "Packages must be a folder, .tbpack, .zip, .tbtheme, or JSON file.");
                }
            }
            else if (!Directory.Exists(sourcePath))
            {
                return ImportResult.Failure($"Source not found: {sourcePath}");
            }

            var manifestPath = explicitManifest
                ?? FindPackageManifest(root);
            PackageManifest? manifest = null;
            var diagnostics = new List<string>();
            if (manifestPath is not null)
            {
                var loaded = await LoadManifestAsync(manifestPath, ct);
                if (loaded.Manifest is null)
                {
                    return ImportResult.Failure(loaded.Errors.ToArray());
                }

                manifest = loaded.Manifest;
                diagnostics.AddRange(loaded.Errors);
            }

            if (manifest is not null
                && !string.IsNullOrWhiteSpace(manifest.MinAppVersion))
            {
                if (!SemanticVersion.TryParse(manifest.MinAppVersion, out var minimum))
                {
                    return ImportResult.Failure(
                        $"Package minAppVersion '{manifest.MinAppVersion}' is invalid.");
                }

                var appVersion = GetAppVersion();
                if (appVersion < minimum)
                {
                    return ImportResult.Failure(
                        $"This package requires TinyBattery {minimum} or newer "
                        + $"(current {appVersion}).");
                }
            }

            var packageRoot = manifestPath is null
                ? root
                : Path.GetDirectoryName(manifestPath) ?? root;

            var deviceItems = new List<PackageItemManifest>(
                manifest?.Devices ?? []);
            var themeItems = new List<PackageItemManifest>(
                manifest?.Themes ?? []);

            if (manifest is null)
            {
                DiscoverItems(root, deviceItems, themeItems);
            }

            if (deviceItems.Count == 0 && themeItems.Count == 0)
            {
                return ImportResult.Failure(
                    "The package contains no device definitions or themes.");
            }

            var validatedDevices = new List<ValidatedDevice>();
            var validatedThemes = new List<ValidatedTheme>();
            var errors = new List<string>();

            foreach (var item in deviceItems)
            {
                ct.ThrowIfCancellationRequested();
                var path = ResolveEntry(packageRoot, item.Path, errors);
                if (path is null)
                {
                    continue;
                }

                var validated = await ValidateDeviceAsync(path, item, errors, ct);
                if (validated is not null)
                {
                    validatedDevices.Add(validated);
                }
            }

            foreach (var item in themeItems)
            {
                ct.ThrowIfCancellationRequested();
                var path = ResolveEntry(packageRoot, item.Path, errors);
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

                var validated = ValidateTheme(path, item, errors);
                if (validated is not null)
                {
                    validatedThemes.Add(validated);
                }
            }

            if (errors.Count > 0)
            {
                return new ImportResult(false, [], errors);
            }

            var duplicateDevice = validatedDevices
                .GroupBy(item => item.Definition.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateDevice is not null)
            {
                return ImportResult.Failure(
                    $"The package contains duplicate device id '{duplicateDevice.Key}'.");
            }

            var duplicateTheme = validatedThemes
                .GroupBy(item => item.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateTheme is not null)
            {
                return ImportResult.Failure(
                    $"The package contains duplicate theme id '{duplicateTheme.Key}'.");
            }

            if (!options.Overwrite)
            {
                foreach (var item in validatedDevices)
                {
                    var destination = Path.Combine(
                        _paths.DefinitionsDirectory,
                        item.Definition.Id + ".json");
                    if (File.Exists(destination))
                    {
                        return ImportResult.Failure(
                            $"Device definition '{item.Definition.Id}' already exists. "
                            + "Enable overwrite to replace it.");
                    }
                }

                foreach (var item in validatedThemes)
                {
                    var destination = Path.Combine(
                        _paths.ThemesDirectory,
                        item.Descriptor.Id);
                    if (Directory.Exists(destination))
                    {
                        return ImportResult.Failure(
                            $"Icon theme '{item.Descriptor.Id}' already exists. "
                            + "Enable overwrite to replace it.");
                    }
                }
            }

            var installedPaths = new List<string>();
            var installedItems = new List<InstalledPackageItem>();
            foreach (var item in validatedDevices)
            {
                var result = await _devices.ImportAsync(item.SourcePath, options, ct);
                if (!result.Success)
                {
                    return new ImportResult(false, installedPaths, result.Diagnostics);
                }

                installedPaths.AddRange(result.InstalledPaths);
                diagnostics.AddRange(result.Diagnostics);
                installedItems.Add(new InstalledPackageItem
                {
                    Kind = "device",
                    Id = item.Definition.Id,
                    Version = item.Definition.Version ?? "0.0.0",
                    Path = result.InstalledPaths.FirstOrDefault(),
                });
            }

            foreach (var item in validatedThemes)
            {
                var result = await _themes.ImportAsync(item.SourcePath, options, ct);
                if (!result.Success)
                {
                    return new ImportResult(false, installedPaths, result.Diagnostics);
                }

                installedPaths.AddRange(result.InstalledPaths);
                diagnostics.AddRange(result.Diagnostics);
                installedItems.Add(new InstalledPackageItem
                {
                    Kind = "theme",
                    Id = item.Descriptor.Id,
                    Version = item.Descriptor.Version ?? "0.0.0",
                    Path = result.InstalledPaths.FirstOrDefault(),
                });
            }

            var packageId = manifest?.Id;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                packageId = "local." + Guid.NewGuid().ToString("N")[..12];
            }

            await _installedPackages.RecordAsync(
                new InstalledPackage
                {
                    Id = packageId,
                    Version = string.IsNullOrWhiteSpace(manifest?.Version)
                        ? "0.0.0"
                        : manifest.Version,
                    Source = options.Source,
                    InstalledAt = DateTimeOffset.UtcNow,
                    Items = installedItems,
                },
                ct);

            _logger.LogInformation(
                "Imported package '{Id}': {Devices} device(s), {Themes} theme(s).",
                packageId,
                validatedDevices.Count,
                validatedThemes.Count);

            diagnostics.Insert(
                0,
                $"已安装 {validatedDevices.Count} 个设备定义、{validatedThemes.Count} 个主题。");
            return new ImportResult(true, installedPaths, diagnostics);
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

    private async Task<ImportResult> ImportSingleJsonAsync(
        string path,
        ImportOptions options,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct);
        }
        catch (IOException exception)
        {
            return ImportResult.Failure(exception.Message);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("devices", out _)
            || root.TryGetProperty("themes", out _)
            || string.Equals(
                Path.GetFileName(path),
                "package.json",
                StringComparison.OrdinalIgnoreCase))
        {
            return await ImportAsync(
                Path.GetDirectoryName(path) ?? ".",
                options,
                ct);
        }

        if (root.TryGetProperty("match", out _)
            || root.TryGetProperty("matches", out _))
        {
            var definition = JsonSerializer.Deserialize<DeviceDefinition>(
                json,
                DefinitionJson.Options);
            var result = await _devices.ImportAsync(path, options, ct);
            if (result.Success)
            {
                await _installedPackages.RecordAsync(
                    new InstalledPackage
                    {
                        Id = "local." + Guid.NewGuid().ToString("N")[..12],
                        Version = definition?.Version ?? "0.0.0",
                        Source = options.Source ?? path,
                        Items =
                        [
                            new InstalledPackageItem
                            {
                                Kind = "device",
                                Id = definition?.Id ?? Path.GetFileNameWithoutExtension(path),
                                Version = definition?.Version ?? "0.0.0",
                                Path = result.InstalledPaths.FirstOrDefault(),
                            },
                        ],
                    },
                    ct);
            }

            return result;
        }

        return ImportResult.Failure(
            "The JSON file is not a device definition or package manifest.");
    }

    private async Task<(PackageManifest? Manifest, List<string> Errors)> LoadManifestAsync(
        string path,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct);
        }
        catch (IOException exception)
        {
            return (null, [exception.Message]);
        }

        var validation = _validator.Validate(json);
        if (!validation.IsValid)
        {
            return (null, validation.Errors.ToList());
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<PackageManifest>(
                json,
                DefinitionJson.Options);
            return manifest is null
                ? (null, ["The package manifest produced an empty result."])
                : (manifest, []);
        }
        catch (JsonException exception)
        {
            return (null, [exception.Message]);
        }
    }

    private async Task<ValidatedDevice?> ValidateDeviceAsync(
        string path,
        PackageItemManifest item,
        List<string> errors,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, ct);
        }
        catch (IOException exception)
        {
            errors.Add(exception.Message);
            return null;
        }

        var validation = _definitionValidator.Validate(json);
        if (!validation.IsValid)
        {
            errors.AddRange(validation.Errors.Select(error => $"{path}: {error}"));
            return null;
        }

        DeviceDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<DeviceDefinition>(
                json,
                DefinitionJson.Options);
        }
        catch (JsonException exception)
        {
            errors.Add($"{path}: {exception.Message}");
            return null;
        }

        if (definition is null || definition.EffectiveMatches.Count == 0)
        {
            errors.Add($"{path}: the definition has no match rules.");
            return null;
        }

        if (!_protocols.TryResolve(definition.Protocol.Kind, out _))
        {
            errors.Add(
                $"{path}: protocol '{definition.Protocol.Kind}' is not registered.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.Id)
            && !string.Equals(item.Id, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{path}: manifest id '{item.Id}' does not match definition id "
                + $"'{definition.Id}'.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.Version)
            && !string.Equals(
                item.Version,
                definition.Version,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{path}: manifest version '{item.Version}' does not match "
                + $"definition version '{definition.Version ?? "(missing)"}'.");
            return null;
        }

        return new ValidatedDevice(path, definition);
    }

    private ValidatedTheme? ValidateTheme(
        string path,
        PackageItemManifest item,
        List<string> errors)
    {
        var manifestPath = Directory.Exists(path)
            ? Path.Combine(path, "theme.json")
            : path;
        if (!File.Exists(manifestPath))
        {
            errors.Add($"{path}: theme.json was not found.");
            return null;
        }

        if (!_themeLoader.TryLoad(manifestPath, out var descriptor, out var themeErrors)
            || descriptor is null)
        {
            errors.AddRange(themeErrors.Select(error => $"{path}: {error}"));
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.Id)
            && !string.Equals(item.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{path}: manifest id '{item.Id}' does not match theme id "
                + $"'{descriptor.Id}'.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.Version)
            && !string.Equals(
                item.Version,
                descriptor.Version,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{path}: manifest version '{item.Version}' does not match "
                + $"theme version '{descriptor.Version ?? "(missing)"}'.");
            return null;
        }

        return new ValidatedTheme(path, descriptor);
    }

    private static void DiscoverItems(
        string root,
        List<PackageItemManifest> devices,
        List<PackageItemManifest> themes)
    {
        foreach (var manifest in Directory.EnumerateFiles(
                     root,
                     "theme.json",
                     SearchOption.AllDirectories))
        {
            themes.Add(new PackageItemManifest
            {
                Path = Path.GetDirectoryName(manifest) ?? root,
            });
        }

        foreach (var file in Directory.EnumerateFiles(
                     root,
                     "*.json",
                     SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "theme.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "package.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "plugin.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var rootElement = document.RootElement;
                if (rootElement.ValueKind == JsonValueKind.Object
                    && (rootElement.TryGetProperty("match", out _)
                        || rootElement.TryGetProperty("matches", out _))
                    && rootElement.TryGetProperty("protocol", out _))
                {
                    devices.Add(new PackageItemManifest { Path = file });
                }
            }
            catch (Exception exception) when (
                exception is IOException or JsonException)
            {
            }
        }
    }

    private static string? FindPackageManifest(string root)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(root, "package.json", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static SemanticVersion GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? typeof(PackageImporter).Assembly.GetName().Version;
        return version is null
            ? new SemanticVersion(0, 0, 0)
            : new SemanticVersion(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build));
    }

    private static string? ResolveEntry(
        string packageRoot,
        string relativePath,
        List<string> diagnostics)    {
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

    private sealed record ValidatedDevice(string SourcePath, DeviceDefinition Definition);

    private sealed record ValidatedTheme(string SourcePath, IconThemeDescriptor Descriptor);
}
