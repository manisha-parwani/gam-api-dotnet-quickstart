namespace GamQuickstart.Resilience;

/// <summary>Settings for <see cref="RetryPolicy"/>.</summary>
public sealed record RetryOptions
{
    /// <summary>Total attempts, including the first one. 1 means "never retry".</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Wait before the first retry. Each later retry waits twice as long.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>The longest single wait, no matter how many retries happened.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);
}
