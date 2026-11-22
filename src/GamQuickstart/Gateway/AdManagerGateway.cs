using System.Security.Cryptography;
using System.ServiceModel;
using GamQuickstart.Configuration;
using GamQuickstart.Paging;
using Google.Api.Ads.AdManager.Lib;
using Google.Api.Ads.AdManager.Util.v202608;
using Google.Api.Ads.Common.Lib;
using AdManagerApi = Google.Api.Ads.AdManager.v202608;

namespace GamQuickstart.Gateway;

/// <summary>
/// The real <see cref="IAdManagerGateway"/>, built on the official Google.Dfp client library.
/// This is the only class that calls the library.
/// </summary>
/// <remarks>
/// To move to a newer API version, change the <c>v202608</c> namespaces in this file and in
/// <see cref="AdManagerErrorTranslator"/>, then update <see cref="ApiVersion"/>.
/// </remarks>
public sealed class AdManagerGateway : IAdManagerGateway
{
    /// <summary>The Ad Manager SOAP API version this gateway uses.</summary>
    public const string ApiVersion = "v202608";

    private readonly AdManagerUser _user;
    private readonly HttpClient _httpClient;

    public AdManagerGateway(AdManagerSettings settings, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;

        var config = new AdManagerAppConfig
        {
            ApplicationName = settings.ApplicationName,
            NetworkCode = settings.NetworkCode,
            OAuth2Mode = OAuth2Flow.SERVICE_ACCOUNT,
        };

        try
        {
            // Setting this path makes the library read client_email and private_key from the JSON key file.
            config.OAuth2SecretsJsonPath = settings.ServiceAccountKeyPath;
        }
        catch (AdsOAuthException ex)
        {
            throw new SettingsException(
                $"Could not read the service account key file '{settings.ServiceAccountKeyPath}': {ex.Message}", ex);
        }

        EnsurePrivateKeyIsValid(config.OAuth2PrivateKey, settings.ServiceAccountKeyPath);
        _user = new AdManagerUser(config);
    }

    /// <summary>
    /// Checks the key early. Otherwise a broken key only fails on the first call, deep inside the auth library.
    /// </summary>
    private static void EnsurePrivateKeyIsValid(string privateKeyPem, string keyPath)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            throw new SettingsException(
                $"The private_key in '{keyPath}' is not a valid PEM private key. Download a new JSON key for the service account.", ex);
        }
    }

    public async Task<NetworkInfo> GetCurrentNetworkAsync(CancellationToken cancellationToken)
    {
        var service = _user.GetService<AdManagerApi.NetworkService>();
        var network = await CallAsync(service, s => s.getCurrentNetworkAsync(), cancellationToken);

        return new NetworkInfo(network.networkCode, network.displayName, network.timeZone, network.currencyCode);
    }

    public async Task<Page<AdUnitSummary>> GetAdUnitsPageAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        // Ordering by ID keeps pages stable while we move the offset forward.
        var statement = new StatementBuilder()
            .OrderBy("id ASC")
            .Limit(limit)
            .Offset(offset)
            .ToStatement();

        var service = _user.GetService<AdManagerApi.InventoryService>();
        var page = await CallAsync(service, s => s.getAdUnitsByStatementAsync(statement), cancellationToken);

        var items = (page.results ?? [])
            .Select(u => new AdUnitSummary(u.id, u.name, u.statusSpecified ? u.status.ToString() : "UNKNOWN"))
            .ToList();
        return new Page<AdUnitSummary>(items, page.totalResultSetSize);
    }

    public async Task<CreatedCreative> CreateImageCreativeAsync(ImageCreativeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var imageBytes = await DownloadImageAsync(request.ImageUrl, cancellationToken);

        var creative = new AdManagerApi.ImageCreative
        {
            advertiserId = request.AdvertiserId,
            name = request.Name,
            size = new AdManagerApi.Size { width = request.Width, height = request.Height, isAspectRatio = false },
            primaryImageAsset = new AdManagerApi.CreativeAsset
            {
                assetByteArray = imageBytes,
                fileName = FileNameFor(request.ImageUrl),
            },
            destinationUrlType = request.ClickThroughUrl is null
                ? AdManagerApi.DestinationUrlType.NONE
                : AdManagerApi.DestinationUrlType.CLICK_TO_WEB,
            destinationUrl = request.ClickThroughUrl?.AbsoluteUri,
        };

        var service = _user.GetService<AdManagerApi.CreativeService>();
        var created = await CallAsync(service, s => s.createCreativesAsync([creative]), cancellationToken);

        var result = created?.FirstOrDefault()
            ?? throw new AdManagerRequestException("createCreatives returned no creative.");
        return new CreatedCreative(result.id, result.name, result.previewUrl);
    }

    /// <summary>
    /// Runs one SOAP call, translates library errors to this app's exception types and always
    /// closes the service client (aborting it if the channel is broken).
    /// </summary>
    private static async Task<TResult> CallAsync<TService, TResult>(
        TService service,
        Func<TService, Task<TResult>> call,
        CancellationToken cancellationToken)
        where TService : ICommunicationObject
    {
        try
        {
            // The generated SOAP methods take no CancellationToken; WaitAsync at least stops waiting.
            return await call(service).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (AdManagerErrorTranslator.Translate(ex) is { } translated)
        {
            throw translated;
        }
        finally
        {
            CloseQuietly(service, cancellationToken.IsCancellationRequested);
        }
    }

    private static void CloseQuietly(ICommunicationObject service, bool abort)
    {
        if (abort || service.State == CommunicationState.Faulted)
        {
            service.Abort();
            return;
        }

        try
        {
            service.Close();
        }
        catch (Exception ex) when (ex is CommunicationException or TimeoutException)
        {
            service.Abort();
        }
    }

    private async Task<byte[]> DownloadImageAsync(Uri imageUrl, CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AdManagerRequestException($"Could not download the image from {imageUrl}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdManagerRequestException($"Downloading the image from {imageUrl} timed out.", ex);
        }

        return bytes.Length > 0
            ? bytes
            : throw new AdManagerRequestException($"The image at {imageUrl} is empty.");
    }

    private static string FileNameFor(Uri imageUrl)
    {
        var fileName = Path.GetFileName(Uri.UnescapeDataString(imageUrl.AbsolutePath));
        return string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;
    }
}
