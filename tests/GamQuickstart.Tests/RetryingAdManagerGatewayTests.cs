using GamQuickstart.Gateway;
using GamQuickstart.Resilience;
using GamQuickstart.Tests.Fakes;

namespace GamQuickstart.Tests;

public class RetryingAdManagerGatewayTests
{
    private readonly FakeAdManagerGateway _inner = new();
    private readonly RetryingAdManagerGateway _gateway;

    public RetryingAdManagerGatewayTests()
    {
        var policy = new RetryPolicy(
            new RetryOptions { MaxAttempts = 3, InitialDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero },
            new ListLogger(),
            delay: (_, _) => Task.CompletedTask);
        _gateway = new RetryingAdManagerGateway(_inner, policy);
    }

    private static ImageCreativeRequest Request() =>
        new(42, "Test creative", new Uri("https://example.com/banner.png"), 300, 250, null);

    [Theory]
    [InlineData(TransientErrorKind.Quota)]
    [InlineData(TransientErrorKind.Server)]
    [InlineData(TransientErrorKind.Network)]
    public async Task Reads_are_retried_on_any_transient_error(TransientErrorKind kind)
    {
        _inner.Failures.Enqueue(new TransientApiException(kind, "temporary"));

        var network = await _gateway.GetCurrentNetworkAsync(TestContext.Current.CancellationToken);

        Assert.Equal("12345678", network.NetworkCode);
        Assert.Equal(2, _inner.NetworkCalls);
    }

    [Fact]
    public async Task Ad_unit_pages_are_retried_with_the_same_offset_and_limit()
    {
        _inner.AdUnits.AddRange(FakeAdManagerGateway.MakeAdUnits(3));
        _inner.Failures.Enqueue(new TransientApiException(TransientErrorKind.Server, "SERVER_BUSY"));

        var page = await _gateway.GetAdUnitsPageAsync(1, 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal([(1, 2), (1, 2)], _inner.AdUnitPageCalls);
    }

    [Fact]
    public async Task Create_is_retried_when_the_request_was_rejected_by_quota()
    {
        _inner.Failures.Enqueue(new TransientApiException(TransientErrorKind.Quota, "EXCEEDED_QUOTA"));

        var created = await _gateway.CreateImageCreativeAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("Test creative", created.Name);
        Assert.Equal(2, _inner.CreatedRequests.Count);
    }

    [Theory]
    [InlineData(TransientErrorKind.Server)]
    [InlineData(TransientErrorKind.Network)]
    public async Task Create_is_not_retried_after_errors_that_might_have_created_the_creative(TransientErrorKind kind)
    {
        _inner.Failures.Enqueue(new TransientApiException(kind, "unknown outcome"));

        await Assert.ThrowsAsync<TransientApiException>(
            () => _gateway.CreateImageCreativeAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Single(_inner.CreatedRequests);
    }
}
