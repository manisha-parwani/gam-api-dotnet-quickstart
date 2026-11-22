using GamQuickstart.Paging;
using GamQuickstart.Resilience;

namespace GamQuickstart.Gateway;

/// <summary>
/// Adds retries to any <see cref="IAdManagerGateway"/> (decorator pattern).
/// </summary>
/// <remarks>
/// Reads are retried on every transient error. Creating a creative is retried only when the API
/// rejected the request (quota), because after a server error or timeout the creative may already
/// exist and a blind retry could create a duplicate.
/// </remarks>
public sealed class RetryingAdManagerGateway(IAdManagerGateway inner, RetryPolicy retryPolicy) : IAdManagerGateway
{
    private readonly IAdManagerGateway _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly RetryPolicy _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));

    public Task<NetworkInfo> GetCurrentNetworkAsync(CancellationToken cancellationToken) =>
        _retryPolicy.ExecuteAsync(
            "NetworkService.getCurrentNetwork",
            _inner.GetCurrentNetworkAsync,
            RetryAnyTransientError,
            cancellationToken);

    public Task<Page<AdUnitSummary>> GetAdUnitsPageAsync(int offset, int limit, CancellationToken cancellationToken) =>
        _retryPolicy.ExecuteAsync(
            "InventoryService.getAdUnitsByStatement",
            ct => _inner.GetAdUnitsPageAsync(offset, limit, ct),
            RetryAnyTransientError,
            cancellationToken);

    public Task<CreatedCreative> CreateImageCreativeAsync(ImageCreativeRequest request, CancellationToken cancellationToken) =>
        _retryPolicy.ExecuteAsync(
            "CreativeService.createCreatives",
            ct => _inner.CreateImageCreativeAsync(request, ct),
            RetryOnlyRejectedRequests,
            cancellationToken);

    private static bool RetryAnyTransientError(TransientApiException _) => true;

    private static bool RetryOnlyRejectedRequests(TransientApiException ex) => ex.RequestWasRejected;
}
