using System.ServiceModel;
using GamQuickstart.Gateway;
using GamQuickstart.Resilience;
using Google.Api.Ads.AdManager.Lib;
using AdManagerApi = Google.Api.Ads.AdManager.v202608;

namespace GamQuickstart.Tests;

/// <summary>
/// Builds the same exception objects the client library throws for SOAP faults, without any network.
/// </summary>
public class AdManagerErrorTranslatorTests
{
    private static AdManagerApiException ApiFault(params AdManagerApi.ApiError[] errors) =>
        new(new AdManagerApi.ApiException { errors = errors }, "SOAP fault");

    [Fact]
    public void Exceeded_quota_is_a_transient_quota_error()
    {
        var fault = ApiFault(new AdManagerApi.QuotaError
        {
            reason = AdManagerApi.QuotaErrorReason.EXCEEDED_QUOTA,
            errorString = "QuotaError.EXCEEDED_QUOTA",
        });

        var translated = Assert.IsType<TransientApiException>(AdManagerErrorTranslator.Translate(fault));

        Assert.Equal(TransientErrorKind.Quota, translated.Kind);
        Assert.True(translated.RequestWasRejected);
        Assert.Equal("QuotaError.EXCEEDED_QUOTA", translated.Message);
        Assert.Same(fault, translated.InnerException);
    }

    [Theory]
    [InlineData(AdManagerApi.ServerErrorReason.SERVER_ERROR)]
    [InlineData(AdManagerApi.ServerErrorReason.SERVER_BUSY)]
    public void Server_errors_are_transient_server_errors(AdManagerApi.ServerErrorReason reason)
    {
        var fault = ApiFault(new AdManagerApi.ServerError { reason = reason, errorString = $"ServerError.{reason}" });

        var translated = Assert.IsType<TransientApiException>(AdManagerErrorTranslator.Translate(fault));

        Assert.Equal(TransientErrorKind.Server, translated.Kind);
        Assert.False(translated.RequestWasRejected);
    }

    [Fact]
    public void Other_api_errors_are_not_retried_and_keep_the_field_path()
    {
        var fault = ApiFault(new AdManagerApi.RequiredError
        {
            reason = AdManagerApi.RequiredErrorReason.REQUIRED,
            errorString = "RequiredError.REQUIRED",
            fieldPath = "creatives[0].advertiserId",
        });

        var translated = Assert.IsType<AdManagerRequestException>(AdManagerErrorTranslator.Translate(fault));

        Assert.Equal("RequiredError.REQUIRED (field: creatives[0].advertiserId)", translated.Message);
    }

    [Fact]
    public void A_quota_error_wins_when_mixed_with_other_errors()
    {
        var fault = ApiFault(
            new AdManagerApi.ServerError { reason = AdManagerApi.ServerErrorReason.SERVER_ERROR, errorString = "ServerError.SERVER_ERROR" },
            new AdManagerApi.QuotaError { reason = AdManagerApi.QuotaErrorReason.EXCEEDED_QUOTA, errorString = "QuotaError.EXCEEDED_QUOTA" });

        var translated = Assert.IsType<TransientApiException>(AdManagerErrorTranslator.Translate(fault));

        Assert.Equal(TransientErrorKind.Quota, translated.Kind);
        Assert.Equal("ServerError.SERVER_ERROR; QuotaError.EXCEEDED_QUOTA", translated.Message);
    }

    [Fact]
    public void Timeouts_and_connection_problems_are_transient_network_errors()
    {
        Assert.Equal(
            TransientErrorKind.Network,
            Assert.IsType<TransientApiException>(AdManagerErrorTranslator.Translate(new TimeoutException("slow"))).Kind);
        Assert.Equal(
            TransientErrorKind.Network,
            Assert.IsType<TransientApiException>(AdManagerErrorTranslator.Translate(new EndpointNotFoundException("no route"))).Kind);
    }

    [Fact]
    public void Other_soap_faults_and_unrelated_exceptions_are_left_alone()
    {
        Assert.Null(AdManagerErrorTranslator.Translate(new FaultException(new FaultReason("some other fault"), new FaultCode("Sender"), action: null)));
        Assert.Null(AdManagerErrorTranslator.Translate(new InvalidOperationException("bug")));
    }
}
