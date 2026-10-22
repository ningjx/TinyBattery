namespace TinyBattery.Abstractions.Devices;

public sealed record DefinitionDiagnostic(
    string Source,
    string? DefinitionId,
    IReadOnlyList<string> Errors);
