using GamQuickstart.Paging;

namespace GamQuickstart.Gateway;

/// <summary>
/// The small set of Ad Manager operations this app needs.
/// </summary>
/// <remarks>
/// Commands only talk to this interface, never to the Google client library directly.
/// That keeps the command logic testable without a network, credentials or SOAP types.
/// </remarks>
public interface IAdManagerGateway
{
    /// <summary>Calls <c>NetworkService.getCurrentNetwork</c>.</summary>
    Task<NetworkInfo> GetCurrentNetworkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Calls <c>InventoryService.getAdUnitsByStatement</c> for one page of ad units,
    /// ordered by ID so that paging is stable.
    /// </summary>
    /// <param name="offset">How many ad units to skip.</param>
    /// <param name="limit">The maximum number of ad units to return.</param>
    /// <param name="cancellationToken">Cancels the wait for the call.</param>
    Task<Page<AdUnitSummary>> GetAdUnitsPageAsync(int offset, int limit, CancellationToken cancellationToken);

    /// <summary>Calls <c>CreativeService.createCreatives</c> with one image creative.</summary>
    Task<CreatedCreative> CreateImageCreativeAsync(ImageCreativeRequest request, CancellationToken cancellationToken);
}
