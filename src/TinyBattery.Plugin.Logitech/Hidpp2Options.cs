namespace TinyBattery.Plugin.Logitech;

internal sealed class Hidpp2Options
{
    public int SoftwareId { get; init; } = 10;

    public int[] DeviceIndexes { get; init; } = [1, 255, 2, 3, 4, 5, 6];

    public bool ScanAllDeviceIndexes { get; init; }

    public string[] BatteryFeatures { get; init; } = ["0x1001", "0x1000", "0x1004"];
}
