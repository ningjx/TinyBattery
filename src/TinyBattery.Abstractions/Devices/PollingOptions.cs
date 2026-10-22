namespace TinyBattery.Abstractions.Devices;

public sealed class PollingOptions
{
    public int IntervalSeconds { get; init; } = 60;

    public int TimeoutSeconds { get; init; } = 2;

    public int JitterSeconds { get; init; } = 5;

    public RetryOptions Retry { get; init; } = new();
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; init; } = 2;

    public int BackoffMs { get; init; } = 300;
}
