using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Core.Serialization;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Importing;

public sealed class DeviceDefinitionImporter : IDeviceDefinitionImporter
{
    private readonly IAppPaths _paths;
    private readonly IProtocolRegistry _protocols;
    private readonly DefinitionValidator _validator;
    private readonly ILogger<DeviceDefinitionImporter> _logger;

    public DeviceDefinitionImporter(
        IAppPaths paths,
        IProtocolRegistry protocols,
        DefinitionValidator validator,
        ILogger<DeviceDefinitionImporter> logger)
    {
        _paths = paths;
        _protocols = protocols;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        string sourcePath,
        ImportOptions options,
        CancellationToken ct)
    {
        if (Directory.Exists(sourcePath))
        {
            var installed = new List<string>();
            var diagnostics = new List<string>();
            foreach (var file in Directory.EnumerateFiles(
                         sourcePath,
                         "*.json",
                         SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                var result = await ImportFileAsync(file, options, ct);
                installed.AddRange(result.InstalledPaths);
                diagnostics.AddRange(result.Diagnostics);
            }

            return new ImportResult(installed.Count > 0, installed, diagnostics);
        }

        if (!File.Exists(sourcePath))
        {
            return ImportResult.Failure($"Source not found: {sourcePath}");
        }

        if (!string.Equals(
                Path.GetExtension(sourcePath),
                ".json",
                StringComparison.OrdinalIgnoreCase))
        {
            return ImportResult.Failure(
                "Device definitions must be imported as .json files or a folder of .json files.");
        }

        return await ImportFileAsync(sourcePath, options, ct);
    }

    private async Task<ImportResult> ImportFileAsync(
        string file,
        ImportOptions options,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(file, ct);
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

        DeviceDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<DeviceDefinition>(
                json,
                DefinitionJson.Options);
        }
        catch (JsonException exception)
        {
            return ImportResult.Failure(exception.Message);
        }

        if (definition is null)
        {
            return ImportResult.Failure(
                $"Device definition '{file}' produced an empty result.");
        }

        if (!_protocols.TryResolve(definition.Protocol.Kind, out _))
        {
            return ImportResult.Failure(
                $"Protocol '{definition.Protocol.Kind}' is not registered. "
                + "Install the plugin that provides this protocol, then import the definition again.");
        }

        Directory.CreateDirectory(_paths.DefinitionsDirectory);
        var destination = Path.Combine(
            _paths.DefinitionsDirectory,
            definition.Id + ".json");

        if (File.Exists(destination) && !options.Overwrite)
        {
            return ImportResult.Failure(
                $"Device definition '{definition.Id}' already exists. Use overwrite to replace it.");
        }

        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temporary, json, ct);
        File.Move(temporary, destination, overwrite: true);

        _logger.LogInformation(
            "Imported device definition '{Id}' to '{Path}'.",
            definition.Id,
            destination);

        return definition.Enabled
            ? ImportResult.Ok(destination)
            : new ImportResult(
                true,
                [destination],
                ["The definition is disabled; it will not be used until enabled."]);
    }
}
