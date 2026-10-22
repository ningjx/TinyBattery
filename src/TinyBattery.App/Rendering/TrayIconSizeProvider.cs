using System.Runtime.InteropServices;

namespace TinyBattery.App.Rendering;

public interface ITrayIconSizeProvider
{
    int GetSize();
}

public sealed partial class TrayIconSizeProvider : ITrayIconSizeProvider
{
    public int GetSize()
    {
        var width = GetSystemMetrics(49);  // SM_CXSMICON
        var height = GetSystemMetrics(50); // SM_CYSMICON
        var size = Math.Max(width, height);
        return size is >= 16 and <= 64 ? size : 16;
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}
