namespace TinyBattery.Abstractions.Themes;

public sealed record ThemeDiagnostic(
    string Source,
    string? ThemeId,
    IReadOnlyList<string> Errors);
