namespace TinyBattery.Abstractions.Settings;

public sealed class UserSettings
{
    public bool StartWithWindows { get; set; }

    public bool NotifyOnLowBattery { get; set; } = true;

    public int LowBatteryThreshold { get; set; } = 20;

    public string? PrimaryDeviceId { get; set; }

    public int DefaultPollIntervalSeconds { get; set; } = 60;

    public string Theme { get; set; } = "System";

    public string BatteryThemeId { get; set; } = "classic";
}
