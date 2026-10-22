using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Themes;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public sealed class BatteryIconService : IBatteryIconService
{
    private readonly IIconThemeStore _themes;
    private readonly IBatteryIconRendererFactory _factory;
    private readonly ITrayIconSizeProvider _sizeProvider;
    private readonly IDefaultTrayIconProvider _defaultIcons;
    private readonly ILogger<BatteryIconService> _logger;
    private readonly object _gate = new();
    private readonly List<BatteryIconCache> _retired = [];

    private BatteryIconCache? _cache;
    private readonly BatteryIconCache _fallbackCache;
    private string _themeId = "classic";

    public BatteryIconService(
        IIconThemeStore themes,
        IBatteryIconRendererFactory factory,
        ITrayIconSizeProvider sizeProvider,
        IDefaultTrayIconProvider defaultIcons,
        ILogger<BatteryIconService> logger)
    {
        _themes = themes;
        _factory = factory;
        _sizeProvider = sizeProvider;
        _defaultIcons = defaultIcons;
        _logger = logger;
        _fallbackCache = new BatteryIconCache(
            new ClassicBatteryIconRenderer(new ClassicIconThemeOptions()));
        _themes.Changed += OnThemesChanged;
        Rebuild();
    }

    public int TrayIconSize => _sizeProvider.GetSize();

    public string ThemeId
    {
        get
        {
            lock (_gate)
            {
                return _themeId;
            }
        }
    }

    public event EventHandler? ThemeChanged;

    public Drawing.Icon GetIcon(BatteryIconState state) =>
        GetIcon(state, TrayIconSize);

    public Drawing.Icon GetIcon(BatteryIconState state, int size)
    {
        lock (_gate)
        {
            try
            {
                return (_cache ?? throw new InvalidOperationException(
                    "The battery icon service has not been initialized.")).Get(state, size);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Icon rendering failed for theme '{ThemeId}'; using the classic fallback.",
                    _themeId);
                return _fallbackCache.Get(state, size);
            }
        }
    }

    public Drawing.Icon GetDefaultIcon() =>
        GetDefaultIcon(TrayIconSize);

    public Drawing.Icon GetDefaultIcon(int size) =>
        _defaultIcons.GetDefaultIcon(size);

    public void Dispose()
    {
        _themes.Changed -= OnThemesChanged;
        lock (_gate)
        {
            _cache?.Dispose();
            _cache = null;
            foreach (var cache in _retired)
            {
                cache.Dispose();
            }

            _retired.Clear();
            _fallbackCache.Dispose();
        }
    }

    private void OnThemesChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var theme = _themes.Selected;
        IBatteryIconRenderer renderer;
        var themeId = theme.Id;

        try
        {
            renderer = _factory.Create(theme);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not create an icon renderer for theme '{ThemeId}'; using classic.",
                theme.Id);
            renderer = new ClassicBatteryIconRenderer(new ClassicIconThemeOptions());
            themeId = "classic";
        }

        lock (_gate)
        {
            if (_cache is not null)
            {
                // Icons already handed to the tray must stay alive; dispose them at shutdown.
                _retired.Add(_cache);
            }

            _cache = new BatteryIconCache(renderer);
            _themeId = themeId;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
