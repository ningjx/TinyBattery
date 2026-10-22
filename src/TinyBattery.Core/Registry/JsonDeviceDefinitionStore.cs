using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Registry;
using TinyBattery.Core.Serialization;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Registry;

public sealed class JsonDeviceDefinitionStore : IDeviceDefinitionStore
{
    private readonly IAppPaths _paths;
    private readonly IPluginAssetCatalog _plugins;
    private readonly DefinitionValidator _validator;
    private readonly ILogger<JsonDeviceDefinitionStore> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private DeviceDefinition[] _definitions = [];
    private DefinitionDiagnostic[] _diagnostics = [];

    public JsonDeviceDefinitionStore(
        IAppPaths paths,
        IPluginAssetCatalog plugins,
        DefinitionValidator validator,
        ILogger<JsonDeviceDefinitionStore> logger)
    {
        _paths = paths;
        _plugins = plugins;
        _validator = validator;
        _logger = logger;
    }

    public IReadOnlyList<DeviceDefinition> Definitions => _definitions;

    public IReadOnlyList<DefinitionDiagnostic> Diagnostics => _diagnostics;

    public event EventHandler? Changed;

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _reloadLock.WaitAsync(ct);
        try
        {
            var definitions = new Dictionary<string, DeviceDefinition>(
                StringComparer.OrdinalIgnoreCase);
            var diagnostics = new List<DefinitionDiagnostic>();

            foreach (var source in _plugins.DeviceDefinitionPaths)
            {
                ct.ThrowIfCancellationRequested();
                string json;
                try
                {
                    json = await File.ReadAllTextAsync(source, ct);
                }
                catch (IOException exception)
                {
                    diagnostics.Add(new DefinitionDiagnostic(
                        source,
                        null,
                        [exception.Message]));
                    continue;
                }

                TryLoad(source, json, definitions, diagnostics);
            }

            if (Directory.Exists(_paths.DefinitionsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(
                             _paths.DefinitionsDirectory,
                             "*.json",
                             SearchOption.TopDirectoryOnly))
                {
                    ct.ThrowIfCancellationRequested();
                    string json;
                    try
                    {
                        json = await File.ReadAllTextAsync(file, ct);
                    }
                    catch (IOException exception)
                    {
                        diagnostics.Add(new DefinitionDiagnostic(
                            file,
                            null,
                            [exception.Message]));
                        continue;
                    }

                    TryLoad(file, json, definitions, diagnostics);
                }
            }

            _definitions = definitions.Values
                .Where(definition => definition.Enabled)
                .OrderByDescending(definition => definition.Priority)
                .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _diagnostics = diagnostics
                .OrderBy(diagnostic => diagnostic.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _logger.LogInformation(
                "Loaded {Count} device definition(s), {DiagnosticCount} diagnostic(s).",
                _definitions.Length,
                _diagnostics.Length);

            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private void TryLoad(
        string source,
        string json,
        Dictionary<string, DeviceDefinition> definitions,
        List<DefinitionDiagnostic> diagnostics)
    {
        var validation = _validator.Validate(json);
        if (!validation.IsValid)
        {
            diagnostics.Add(new DefinitionDiagnostic(source, null, validation.Errors));
            _logger.LogWarning(
                "Device definition '{Source}' is invalid: {Errors}",
                source,
                string.Join(" | ", validation.Errors));
            return;
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
            diagnostics.Add(new DefinitionDiagnostic(source, null, [exception.Message]));
            _logger.LogWarning(
                "Device definition '{Source}' could not be deserialized: {Error}",
                source,
                exception.Message);
            return;
        }

        if (definition is null)
        {
            diagnostics.Add(new DefinitionDiagnostic(
                source,
                null,
                ["The definition produced an empty result."]));
            return;
        }

        if (definition.SchemaVersion != 1)
        {
            diagnostics.Add(new DefinitionDiagnostic(
                source,
                definition.Id,
                [$"Unsupported schemaVersion {definition.SchemaVersion}; expected 1."]));
            return;
        }

        if (definition.Polling is null)
        {
            definition = definition with { Polling = new PollingOptions() };
        }

        // Later sources (user files) override earlier sources (plugin definitions).
        definitions[definition.Id] = definition;
    }
}
