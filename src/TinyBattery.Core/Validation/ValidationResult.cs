namespace TinyBattery.Core.Validation;

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
