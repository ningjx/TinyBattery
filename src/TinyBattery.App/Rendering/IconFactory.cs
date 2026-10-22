using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace TinyBattery.App.Rendering;

public static partial class IconFactory
{
    public static Drawing.Icon FromBitmap(Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            // FromHandle does not take ownership of HICON; clone it so Icon.Dispose owns a copy.
            using var borrowed = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);
}
