using System.Runtime.InteropServices;

namespace TinyBattery.Infrastructure.Hid;

internal static partial class NativeHidInfo
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int HidpStatusSuccess = 0x00110000;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static HidCaps? TryGetCaps(string devicePath)
    {
        var handle = CreateFileW(
            devicePath,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle == InvalidHandleValue)
        {
            return null;
        }

        IntPtr preparsed = IntPtr.Zero;
        IntPtr caps = IntPtr.Zero;
        try
        {
            if (!HidD_GetPreparsedData(handle, out preparsed) || preparsed == IntPtr.Zero)
            {
                return null;
            }

            caps = Marshal.AllocHGlobal(64);
            if (HidP_GetCaps(preparsed, caps) != HidpStatusSuccess)
            {
                return null;
            }

            return new HidCaps(
                (ushort)Marshal.ReadInt16(caps, 0),
                (ushort)Marshal.ReadInt16(caps, 2),
                (ushort)Marshal.ReadInt16(caps, 4),
                (ushort)Marshal.ReadInt16(caps, 6),
                (ushort)Marshal.ReadInt16(caps, 8));
        }
        finally
        {
            if (caps != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(caps);
            }

            if (preparsed != IntPtr.Zero)
            {
                HidD_FreePreparsedData(preparsed);
            }

            CloseHandle(handle);
        }
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    private static partial IntPtr CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_GetPreparsedData(IntPtr device, out IntPtr preparsedData);

    [LibraryImport("hid.dll")]
    private static partial int HidP_GetCaps(IntPtr preparsedData, IntPtr capabilities);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HidD_FreePreparsedData(IntPtr preparsedData);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}

internal readonly record struct HidCaps(
    ushort Usage,
    ushort UsagePage,
    ushort InputReportByteLength,
    ushort OutputReportByteLength,
    ushort FeatureReportByteLength);
