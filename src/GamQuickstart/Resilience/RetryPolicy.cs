using Microsoft.Extensions.Logging;

namespace GamQuickstart.Resilience;

/// <summary>
/// Retries an operation on <see cref="TransientApiException"/> with exponential backoff.
/// </summary>
/// <remarks>
/// Waits are InitialDelay, 2x, 4x, ... capped at MaxDelay, for at most MaxAttempts attempts.
/// Any other exception is not retried. The wait function is injectable so tests run instantly.
/// </remarks>
public sealed partial class RetryPolicy
{
    private readonly RetryOptions _options;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RetryPolicy(RetryOptions options, ILogger logger, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1, nameof(options.MaxAttempts));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.InitialDelay, TimeSpan.Zero, nameof(options.InitialDelay));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxDelay, options.InitialDelay, nameof(options.MaxDelay));

        _options = options;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>The wait after failed attempt number <paramref name="attempt"/> (1-based).</summary>
    public TimeSpan GetDelay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var milliseconds = _options.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, _options.MaxDelay.TotalMilliseconds));
    }

    /// <summary>Runs <paramref name="action"/>, retrying transient failures.</summary>
    /// <param name="operationName">A short name for log messages, for example the API method.</param>
    /// <param name="action">The operation to run.</param>
    /// <param name="shouldRetry">Decides if a given transient failure may be retried.</param>
    /// <param name="cancellationToken">Cancels the operation and any wait between attempts.</param>
    public async Task<T> ExecuteAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> action,
        Func<TransientApiException, bool> shouldRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (TransientApiException ex) when (attempt < _options.MaxAttempts && shouldRetry(ex))
            {
                var delay = GetDelay(attempt);
                LogRetry(operationName, ex.Kind, attempt, _options.MaxAttempts, delay.TotalSeconds, ex.Message);
                await _delay(delay, cancellationToken);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Operation} failed with a transient {Kind} error on attempt {Attempt} of {MaxAttempts}. "
            + "Retrying in {DelaySeconds:0.###}s. Error: {Error}")]
    private partial void LogRetry(
        string operation, TransientErrorKind kind, int attempt, int maxAttempts, double delaySeconds, string error);
}
