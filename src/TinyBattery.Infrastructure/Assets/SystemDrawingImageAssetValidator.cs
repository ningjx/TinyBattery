using System.Drawing;
using System.IO;
using TinyBattery.Abstractions.Assets;

namespace TinyBattery.Infrastructure.Assets;

public sealed class SystemDrawingImageAssetValidator : IImageAssetValidator
{
    private const int MaxDimension = 1024;

    public bool TryValidate(string path, out string? error)
    {
        error = null;
        try
        {
            if (string.Equals(
                    Path.GetExtension(path),
                    ".ico",
                    StringComparison.OrdinalIgnoreCase))
            {
                using var icon = new Icon(path);
                return ValidateSize(icon.Width, icon.Height, path, out error);
            }

            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(
                stream,
                useEmbeddedColorManagement: false,
                validateImageData: true);
            return ValidateSize(image.Width, image.Height, path, out error);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or OutOfMemoryException)
        {
            error = $"Image '{Path.GetFileName(path)}' could not be decoded: {exception.Message}";
            return false;
        }
    }

    private static bool ValidateSize(
        int width,
        int height,
        string path,
        out string? error)
    {
        if (width is < 1 or > MaxDimension || height is < 1 or > MaxDimension)
        {
            error = $"Image '{Path.GetFileName(path)}' has unsupported dimensions {width}x{height}; "
                + $"expected 1..{MaxDimension}.";
            return false;
        }

        error = null;
        return true;
    }
}
