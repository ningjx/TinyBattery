namespace TinyBattery.Abstractions.Settings;

public sealed class UserSettings
{
    public bool StartWithWindows { get; set; }

    public bool NotifyOnLowBattery { get; set; } = true;

    public int LowBatteryThreshold { get; set; } = 20;

    public string? PrimaryDeviceId { get; set; }

    public List<string> DisplayDeviceIds { get; set; } = [];

    public int DefaultPollIntervalSeconds { get; set; } = 60;

    public string Theme { get; set; } = "System";

    public string BatteryThemeId { get; set; } = "classic";

    public string? UpdateFeedUrl { get; set; }

    public string UpdateChannel { get; set; } = "stable";

    public bool CheckForUpdatesOnStartup { get; set; } = true;

    public bool AutoUpdate { get; set; }
}
