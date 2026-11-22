using System.ServiceModel;
using GamQuickstart.Resilience;
using Google.Api.Ads.AdManager.Lib;
using Google.Api.Ads.Common.Lib;
using AdManagerApi = Google.Api.Ads.AdManager.v202608;

namespace GamQuickstart.Gateway;

/// <summary>
/// Turns Google client library and WCF errors into this app's own exception types,
/// so the rest of the code (and the retry logic) never needs SOAP types.
/// </summary>
public static class AdManagerErrorTranslator
{
    /// <summary>
    /// Returns a <see cref="TransientApiException"/> for errors worth retrying, an
    /// <see cref="AdManagerRequestException"/> for other API errors, or null to leave the exception unchanged.
    /// </summary>
    public static Exception? Translate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            AdManagerApiException { ApiException: AdManagerApi.ApiException apiException } =>
                FromApiException(apiException, exception),

            AdsOAuthException => FromOAuthException(exception),

            TimeoutException =>
                new TransientApiException(TransientErrorKind.Network, exception.Message, exception),

            // FaultException is a CommunicationException too, but it means the server answered with an error.
            CommunicationException and not FaultException =>
                new TransientApiException(TransientErrorKind.Network, exception.Message, exception),

            _ => null,
        };
    }

    private static Exception FromApiException(AdManagerApi.ApiException apiException, Exception original)
    {
        var errors = apiException.errors ?? [];
        var details = Describe(errors, original.Message);

        if (errors.OfType<AdManagerApi.QuotaError>().Any(e => e.reason == AdManagerApi.QuotaErrorReason.EXCEEDED_QUOTA))
        {
            return new TransientApiException(TransientErrorKind.Quota, details, original);
        }

        if (errors.OfType<AdManagerApi.ServerError>().Any(e =>
                e.reason is AdManagerApi.ServerErrorReason.SERVER_ERROR or AdManagerApi.ServerErrorReason.SERVER_BUSY))
        {
            return new TransientApiException(TransientErrorKind.Server, details, original);
        }

        return new AdManagerRequestException(details, original);
    }

    private static AdManagerRequestException FromOAuthException(Exception exception)
    {
        // The inner exception usually holds the useful part, for example "invalid_grant".
        var reason = exception.InnerException is { } inner ? $" ({inner.Message})" : string.Empty;
        return new AdManagerRequestException(
            $"Authentication failed: {exception.Message}{reason} Check the key file and that the service "
            + "account was added as a user in your Ad Manager network.",
            exception);
    }

    private static string Describe(AdManagerApi.ApiError[] errors, string fallback) =>
        errors.Length == 0
            ? fallback
            : string.Join("; ", errors.Select(e => string.IsNullOrEmpty(e.fieldPath) ? e.errorString : $"{e.errorString} (field: {e.fieldPath})"));
}
