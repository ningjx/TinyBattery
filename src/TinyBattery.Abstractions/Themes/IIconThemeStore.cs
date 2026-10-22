namespace TinyBattery.Abstractions.Themes;

public interface IIconThemeStore
{
    IReadOnlyList<IconThemeDescriptor> Themes { get; }

    IconThemeDescriptor Selected { get; }

    IReadOnlyList<ThemeDiagnostic> Diagnostics { get; }

    event EventHandler? Changed;

    Task ReloadAsync(CancellationToken ct);

    Task SelectAsync(string themeId, CancellationToken ct);
}
