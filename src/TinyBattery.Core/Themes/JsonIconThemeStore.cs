using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Plugins;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Themes;

namespace TinyBattery.Core.Themes;

public sealed class JsonIconThemeStore : IIconThemeStore
{
    private readonly IAppPaths _paths;
    private readonly IPluginAssetCatalog _plugins;
    private readonly IUserSettingsStore _settings;
    private readonly IconThemeLoader _loader;
    private readonly ILogger<JsonIconThemeStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IconThemeDescriptor[] _themes = [IconThemeDescriptor.CreateFallback()];
    private IconThemeDescriptor _selected = IconThemeDescriptor.CreateFallback();
    private ThemeDiagnostic[] _diagnostics = [];

    public JsonIconThemeStore(
        IAppPaths paths,
        IPluginAssetCatalog plugins,
        IUserSettingsStore settings,
        IconThemeLoader loader,
        ILogger<JsonIconThemeStore> logger)
    {
        _paths = paths;
        _plugins = plugins;
        _settings = settings;
        _loader = loader;
        _logger = logger;
    }

    public IReadOnlyList<IconThemeDescriptor> Themes => _themes;

    public IconThemeDescriptor Selected => _selected;

    public IReadOnlyList<ThemeDiagnostic> Diagnostics => _diagnostics;

    public event EventHandler? Changed;

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var themes = new Dictionary<string, IconThemeDescriptor>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["fallback"] = IconThemeDescriptor.CreateFallback(),
            };
            var diagnostics = new List<ThemeDiagnostic>();

            foreach (var manifestPath in _plugins.ThemeManifestPaths)
            {
                ct.ThrowIfCancellationRequested();
                LoadManifest(manifestPath, themes, diagnostics);
            }

            foreach (var plugin in _plugins.ThemePlugins)
            {
                ct.ThrowIfCancellationRequested();
                var theme = plugin.Describe();
                if (theme.Kind != IconThemeKind.Svg)
                {
                    _logger.LogInformation(
                        "Ignoring legacy plugin theme '{ThemeId}' ({Kind}).",
                        theme.Id,
                        theme.Kind);
                    continue;
                }

                if (string.Equals(theme.Id, "fallback", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ThemeDiagnostic(
                        plugin.GetType().FullName ?? "plugin",
                        theme.Id,
                        ["Theme id 'fallback' is reserved."]));
                    continue;
                }

                themes[theme.Id] = theme;
            }

            if (Directory.Exists(_paths.ThemesDirectory))
            {
                foreach (var manifestPath in Directory.EnumerateFiles(
                             _paths.ThemesDirectory,
                             "theme.json",
                             SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    LoadManifest(manifestPath, themes, diagnostics);
                }
            }

            _themes = themes.Values
                .OrderBy(theme => string.Equals(theme.Id, "fallback", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
                .ThenBy(theme => theme.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var selectedId = string.IsNullOrWhiteSpace(_settings.Current.BatteryThemeId)
                ? "classic"
                : _settings.Current.BatteryThemeId;
            if (!themes.TryGetValue(selectedId, out var selected))
            {
                selected = themes["fallback"];
                diagnostics.Add(new ThemeDiagnostic(
                    _paths.SettingsFile,
                    selectedId,
                    [$"Theme '{selectedId}' was not found; falling back to 'fallback'."]));
            }

            _selected = selected;
            _diagnostics = diagnostics
                .OrderBy(diagnostic => diagnostic.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _logger.LogInformation(
                "Loaded {Count} icon theme(s), selected '{Selected}'.",
                _themes.Length,
                _selected.Id);

            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SelectAsync(string themeId, CancellationToken ct)
    {
        var theme = _themes.FirstOrDefault(
            candidate => string.Equals(candidate.Id, themeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException(
                $"Icon theme '{themeId}' is not loaded.",
                nameof(themeId));

        await _gate.WaitAsync(ct);
        try
        {
            _settings.Current.BatteryThemeId = theme.Id;
            await _settings.SaveAsync(_settings.Current, ct);
            _selected = theme;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void LoadManifest(
        string manifestPath,
        Dictionary<string, IconThemeDescriptor> themes,
        List<ThemeDiagnostic> diagnostics)
    {
        if (!_loader.TryLoad(manifestPath, out var theme, out var errors))
        {
            diagnostics.Add(new ThemeDiagnostic(manifestPath, null, errors));
            _logger.LogWarning(
                "Icon theme '{Path}' is invalid: {Errors}",
                manifestPath,
                string.Join(" | ", errors));
            return;
        }

        if (theme is null)
        {
            return;
        }

        if (theme.Kind != IconThemeKind.Svg)
        {
            _logger.LogInformation(
                "Ignoring legacy theme '{Path}' ({Kind}); only SVG themes are selectable.",
                manifestPath,
                theme.Kind);
            return;
        }

        if (string.Equals(theme.Id, "fallback", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ThemeDiagnostic(
                manifestPath,
                theme.Id,
                ["Theme id 'fallback' is reserved."]));
            return;
        }

        themes[theme.Id] = theme;
    }
}
