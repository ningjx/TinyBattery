using TinyBattery.Abstractions.Assets;

namespace TinyBattery.Core.Tests;

internal sealed class FakeImageAssetValidator : IImageAssetValidator
{
    public bool IsValid { get; set; } = true;

    public bool TryValidate(string path, out string? error)
    {
        error = IsValid ? null : "invalid image data";
        return IsValid;
    }
}
