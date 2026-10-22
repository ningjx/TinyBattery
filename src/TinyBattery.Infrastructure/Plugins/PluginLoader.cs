using System.Reflection;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Protocols;

namespace TinyBattery.Infrastructure.Plugins;

public sealed class PluginLoader : IPluginLoader
{
    private const int SupportedApiMajor = 1;

    private readonly IAppPaths _paths;
    private readonly PluginAssetCatalog _catalog;
    private readonly IProtocolRegistry _registry;
    private readonly PluginManifestValidator _validator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<PluginLoadContext> _contexts = [];

    public PluginLoader(
        IAppPaths paths,
        PluginAssetCatalog catalog,
        IProtocolRegistry registry,
        PluginManifestValidator validator,
        ILoggerFactory loggerFactory,
        ILogger<PluginLoader> logger)
    {
        _paths = paths;
        _catalog = catalog;
        _registry = registry;
        _validator = validator;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public Task LoadAsync(CancellationToken ct)
    {
        var loadedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetRoots())
        {
            ct.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in EnumeratePluginDirectories(root))
            {
                ct.ThrowIfCancellationRequested();
                if (!visitedDirectories.Add(Path.GetFullPath(directory)))
                {
                    continue;
                }

                ProcessPlugin(directory, loadedIds);
            }
        }

        _logger.LogInformation(
            "Loaded {PluginCount} plugin(s), {ProtocolCount} protocol(s), {ThemeCount} theme manifest(s), {DiagnosticCount} diagnostic(s).",
            loadedIds.Count,
            _catalog.Protocols.Count,
            _catalog.ThemeManifestPaths.Count,
            _catalog.Diagnostics.Count);

        return Task.CompletedTask;
    }

    private IEnumerable<string> GetRoots()
    {
        yield return _paths.PluginsDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "plugins");
    }

    private static IEnumerable<string> EnumeratePluginDirectories(string root)
    {
        if (File.Exists(Path.Combine(root, "plugin.json")))
        {
            yield return root;
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     root,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.Combine(directory, "plugin.json")))
            {
                yield return directory;
            }
        }
    }

    private void ProcessPlugin(string directory, HashSet<string> loadedIds)
    {
        var manifestPath = Path.Combine(directory, "plugin.json");
        PluginManifest? manifest;
        IReadOnlyList<string> errors;
        try
        {
            manifest = null;
            if (!_validator.TryLoad(
                    File.ReadAllText(manifestPath),
                    out manifest,
                    out errors))
            {
                AddDiagnostic(manifestPath, string.Join(" | ", errors));
                return;
            }
        }
        catch (IOException exception)
        {
            AddDiagnostic(manifestPath, exception.Message);
            return;
        }

        if (manifest is null)
        {
            AddDiagnostic(manifestPath, "The plugin manifest produced an empty result.");
            return;
        }

        if (!loadedIds.Add(manifest.Id))
        {
            AddDiagnostic(
                manifestPath,
                $"Plugin id '{manifest.Id}' is already loaded; skipping '{directory}'.");
            return;
        }

        try
        {
            foreach (var device in manifest.Devices ?? [])
            {
                string? deviceError = null;
                foreach (var file in PluginPathResolver.ExpandDeviceDefinitions(
                             directory,
                             device,
                             out deviceError))
                {
                    _catalog.AddDeviceDefinitionPath(file);
                }

                if (deviceError is not null)
                {
                    AddDiagnostic(manifestPath, deviceError);
                }
            }

            foreach (var theme in manifest.Themes ?? [])
            {
                string? themeError = null;
                foreach (var file in PluginPathResolver.ExpandThemeManifests(
                             directory,
                             theme,
                             out themeError))
                {
                    _catalog.AddThemeManifestPath(file);
                }

                if (themeError is not null)
                {
                    AddDiagnostic(manifestPath, themeError);
                }
            }

            if (!string.IsNullOrWhiteSpace(manifest.Assembly)
                && !string.IsNullOrWhiteSpace(manifest.EntryType))
            {
                LoadCodePlugin(directory, manifest, manifestPath);
            }

            _logger.LogInformation(
                "Loaded plugin '{PluginId}' ({PluginName}) from '{Directory}'.",
                manifest.Id,
                manifest.Name,
                directory);
        }
        catch (Exception exception)
        {
            AddDiagnostic(manifestPath, exception.Message);
            _logger.LogError(
                exception,
                "Plugin '{PluginId}' failed to load.",
                manifest.Id);
        }
    }

    private void LoadCodePlugin(
        string directory,
        PluginManifest manifest,
        string manifestPath)
    {
        var assemblyPath = PluginPathResolver.Resolve(
            directory,
            manifest.Assembly!,
            out var error);
        if (assemblyPath is null)
        {
            AddDiagnostic(manifestPath, error ?? "Plugin assembly was not found.");
            return;
        }

        if (!string.Equals(
                Path.GetExtension(assemblyPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(manifestPath, "Plugin assembly must be a .dll.");
            return;
        }

        var context = new PluginLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        _contexts.Add(context);

        var type = assembly.GetType(manifest.EntryType!, throwOnError: false);
        if (type is null || !typeof(ITinyBatteryPlugin).IsAssignableFrom(type))
        {
            AddDiagnostic(
                manifestPath,
                $"Entry type '{manifest.EntryType}' does not implement ITinyBatteryPlugin.");
            return;
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            AddDiagnostic(
                manifestPath,
                $"Entry type '{manifest.EntryType}' needs a public parameterless constructor.");
            return;
        }

        var plugin = (ITinyBatteryPlugin)Activator.CreateInstance(type)!;
        if (plugin.ApiVersion.Major != SupportedApiMajor)
        {
            AddDiagnostic(
                manifestPath,
                $"Plugin '{plugin.Id}' targets API {plugin.ApiVersion}; this host supports {SupportedApiMajor}.x.");
            return;
        }

        if (!string.Equals(plugin.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Plugin manifest id '{ManifestId}' differs from plugin id '{PluginId}'.",
                manifest.Id,
                plugin.Id);
        }

        var pluginContext = new PluginContext(directory, _loggerFactory);
        var registrar = new PluginRegistrar(
            _catalog,
            _registry,
            directory,
            _loggerFactory.CreateLogger($"Plugin.{manifest.Id}"));

        plugin.Register(pluginContext, registrar);
    }

    private void AddDiagnostic(string source, string message)
    {
        _catalog.AddDiagnostic(source, message);
        _logger.LogWarning("Plugin diagnostic at '{Source}': {Message}", source, message);
    }
}
