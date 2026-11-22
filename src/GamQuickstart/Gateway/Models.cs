namespace GamQuickstart.Gateway;

/// <summary>The fields of an Ad Manager network that the <c>network</c> command prints.</summary>
public sealed record NetworkInfo(string NetworkCode, string DisplayName, string TimeZone, string CurrencyCode);

/// <summary>The fields of an ad unit that the <c>adunits</c> command prints.</summary>
public sealed record AdUnitSummary(string Id, string Name, string Status);

/// <summary>Everything needed to create one image creative.</summary>
/// <param name="AdvertiserId">The ID of the advertiser company that owns the creative.</param>
/// <param name="Name">The creative name shown in Ad Manager.</param>
/// <param name="ImageUrl">Where to download the image from. The bytes are uploaded to Ad Manager.</param>
/// <param name="Width">Creative width in pixels.</param>
/// <param name="Height">Creative height in pixels.</param>
/// <param name="ClickThroughUrl">Optional landing page. When null, clicking the creative does nothing.</param>
public sealed record ImageCreativeRequest(
    long AdvertiserId,
    string Name,
    Uri ImageUrl,
    int Width,
    int Height,
    Uri? ClickThroughUrl);

/// <summary>The result of a successful <c>createCreatives</c> call.</summary>
public sealed record CreatedCreative(long Id, string Name, string? PreviewUrl);
