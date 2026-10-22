namespace TinyBattery.Abstractions.Assets;

public interface IImageAssetValidator
{
    bool TryValidate(string path, out string? error);
}
