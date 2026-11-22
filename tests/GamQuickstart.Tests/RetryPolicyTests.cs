using GamQuickstart.Resilience;
using GamQuickstart.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace GamQuickstart.Tests;

public class RetryPolicyTests
{
    private readonly List<TimeSpan> _delays = [];
    private readonly ListLogger _logger = new();

    private RetryPolicy CreatePolicy(int maxAttempts = 5, double initialSeconds = 1, double maxSeconds = 30) =>
        new(
            new RetryOptions
            {
                MaxAttempts = maxAttempts,
                InitialDelay = TimeSpan.FromSeconds(initialSeconds),
                MaxDelay = TimeSpan.FromSeconds(maxSeconds),
            },
            _logger,
            delay: (delay, _) =>
            {
                _delays.Add(delay);
                return Task.CompletedTask;
            });

    private static Func<TransientApiException, bool> Always => _ => true;

    [Fact]
    public async Task Succeeds_after_transient_failures_with_exponential_backoff()
    {
        var policy = CreatePolicy();
        var calls = 0;

        var result = await policy.ExecuteAsync(
            "op",
            _ =>
            {
                calls++;
                return calls < 4
                    ? throw new TransientApiException(TransientErrorKind.Server, "busy")
                    : Task.FromResult("ok");
            },
            Always,
            TestContext.Current.CancellationToken);

        Assert.Equal("ok", result);
        Assert.Equal(4, calls);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], _delays);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 16)]
    [InlineData(6, 30)] // 32s is capped at 30s
    [InlineData(100, 30)]
    public void Delay_doubles_each_attempt_and_is_capped(int attempt, double expectedSeconds)
    {
        var policy = CreatePolicy(initialSeconds: 1, maxSeconds: 30);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), policy.GetDelay(attempt));
    }

    [Fact]
    public async Task Gives_up_after_max_attempts_and_rethrows_the_last_error()
    {
        var policy = CreatePolicy(maxAttempts: 3);
        var calls = 0;

        var ex = await Assert.ThrowsAsync<TransientApiException>(() => policy.ExecuteAsync<string>(
            "op",
            _ =>
            {
                calls++;
                throw new TransientApiException(TransientErrorKind.Quota, $"quota {calls}");
            },
            Always,
            TestContext.Current.CancellationToken));

        Assert.Equal("quota 3", ex.Message);
        Assert.Equal(3, calls);
        Assert.Equal(2, _delays.Count);
    }

    [Fact]
    public async Task Does_not_retry_errors_that_are_not_transient()
    {
        var policy = CreatePolicy();
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync<string>(
            "op",
            _ =>
            {
                calls++;
                throw new InvalidOperationException("bad request");
            },
            Always,
            TestContext.Current.CancellationToken));

        Assert.Equal(1, calls);
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task Does_not_retry_when_the_caller_says_no()
    {
        var policy = CreatePolicy();
        var calls = 0;

        await Assert.ThrowsAsync<TransientApiException>(() => policy.ExecuteAsync<string>(
            "op",
            _ =>
            {
                calls++;
                throw new TransientApiException(TransientErrorKind.Server, "server error");
            },
            shouldRetry: _ => false,
            TestContext.Current.CancellationToken));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Logs_a_warning_for_each_retry()
    {
        var policy = CreatePolicy(maxAttempts: 3);
        var calls = 0;

        await policy.ExecuteAsync(
            "InventoryService.getAdUnitsByStatement",
            _ => ++calls < 3 ? throw new TransientApiException(TransientErrorKind.Quota, "EXCEEDED_QUOTA") : Task.FromResult(1),
            Always,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, _logger.Entries.Count);
        Assert.All(_logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.Contains("InventoryService.getAdUnitsByStatement", _logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("attempt 1 of 3", _logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("Retrying in 1s", _logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("attempt 2 of 3", _logger.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Max_attempts_of_one_means_no_retries()
    {
        var policy = CreatePolicy(maxAttempts: 1);

        await Assert.ThrowsAsync<TransientApiException>(() => policy.ExecuteAsync<int>(
            "op",
            _ => throw new TransientApiException(TransientErrorKind.Network, "timeout"),
            Always,
            TestContext.Current.CancellationToken));

        Assert.Empty(_delays);
    }

    [Fact]
    public void Invalid_options_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(new RetryOptions { MaxAttempts = 0 }, _logger));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(
            new RetryOptions { InitialDelay = TimeSpan.FromSeconds(10), MaxDelay = TimeSpan.FromSeconds(1) },
            _logger));
    }
}
