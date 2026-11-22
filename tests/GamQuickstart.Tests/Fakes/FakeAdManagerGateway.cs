using GamQuickstart.Gateway;
using GamQuickstart.Paging;

namespace GamQuickstart.Tests.Fakes;

/// <summary>An in-memory gateway that records calls and can be told to fail.</summary>
internal sealed class FakeAdManagerGateway : IAdManagerGateway
{
    public NetworkInfo Network { get; set; } = new("12345678", "Test network", "Europe/Helsinki", "EUR");

    public List<AdUnitSummary> AdUnits { get; } = [];

    public List<(int Offset, int Limit)> AdUnitPageCalls { get; } = [];

    public List<ImageCreativeRequest> CreatedRequests { get; } = [];

    public int NetworkCalls { get; private set; }

    /// <summary>Exceptions to throw, one per call, before calls start to succeed.</summary>
    public Queue<Exception> Failures { get; } = new();

    public Task<NetworkInfo> GetCurrentNetworkAsync(CancellationToken cancellationToken)
    {
        NetworkCalls++;
        ThrowNextFailure();
        return Task.FromResult(Network);
    }

    public Task<Page<AdUnitSummary>> GetAdUnitsPageAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        AdUnitPageCalls.Add((offset, limit));
        ThrowNextFailure();
        var items = AdUnits.Skip(offset).Take(limit).ToList();
        return Task.FromResult(new Page<AdUnitSummary>(items, AdUnits.Count));
    }

    public Task<CreatedCreative> CreateImageCreativeAsync(ImageCreativeRequest request, CancellationToken cancellationToken)
    {
        CreatedRequests.Add(request);
        ThrowNextFailure();
        return Task.FromResult(new CreatedCreative(9000 + CreatedRequests.Count, request.Name, "https://preview.example/creative"));
    }

    public static List<AdUnitSummary> MakeAdUnits(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new AdUnitSummary(i.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Ad unit {i}", "ACTIVE"))
            .ToList();

    private void ThrowNextFailure()
    {
        if (Failures.TryDequeue(out var failure))
        {
            throw failure;
        }
    }
}
