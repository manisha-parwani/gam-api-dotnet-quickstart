using GamQuickstart.Commands;
using GamQuickstart.Configuration;
using GamQuickstart.Gateway;
using GamQuickstart.Resilience;
using GamQuickstart.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamQuickstart.Tests;

public class CliAppTests
{
    private readonly FakeAdManagerGateway _gateway = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private Func<IAdManagerGateway> _factory;
    private int _gatewaysCreated;

    public CliAppTests() =>
        _factory = () =>
        {
            _gatewaysCreated++;
            return _gateway;
        };

    private Task<int> RunAsync(params string[] args) =>
        new CliApp(_factory, _output, _error, NullLoggerFactory.Instance, TimeProvider.System)
            .RunAsync(args, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Invalid_arguments_give_exit_code_2_and_never_touch_the_api()
    {
        var exitCode = await RunAsync("adunits", "--top", "zero");

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Contains("Error: --top must be a whole number", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, _gatewaysCreated);
    }

    [Fact]
    public async Task Help_prints_usage()
    {
        var exitCode = await RunAsync("help");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Usage:", _output.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, _gatewaysCreated);
    }

    [Fact]
    public async Task A_dry_run_works_without_any_settings()
    {
        _factory = () => throw new SettingsException("should not be needed");

        var exitCode = await RunAsync(
            "create-image-creative", "--advertiser-id", "1", "--image-url", "https://example.com/a.png", "--width", "1", "--height", "1");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("DRY RUN", _output.ToString(), StringComparison.Ordinal);
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public async Task Runs_the_network_command()
    {
        var exitCode = await RunAsync("network");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Network code:  12345678", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settings_problems_are_reported_with_exit_code_1()
    {
        _factory = () => throw new SettingsException("- The network code is missing.");

        var exitCode = await RunAsync("network");

        Assert.Equal(ExitCodes.Error, exitCode);
        Assert.Contains("Configuration problem:", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("The network code is missing.", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transient_errors_that_outlast_the_retries_are_reported()
    {
        _gateway.Failures.Enqueue(new TransientApiException(TransientErrorKind.Quota, "QuotaError.EXCEEDED_QUOTA"));

        var exitCode = await RunAsync("network");

        Assert.Equal(ExitCodes.Error, exitCode);
        Assert.Contains("temporarily unavailable", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("QuotaError.EXCEEDED_QUOTA", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_errors_are_reported()
    {
        _gateway.Failures.Enqueue(new AdManagerRequestException("PermissionError.PERMISSION_DENIED"));

        var exitCode = await RunAsync("adunits");

        Assert.Equal(ExitCodes.Error, exitCode);
        Assert.Contains("The Ad Manager request failed: PermissionError.PERMISSION_DENIED", _error.ToString(), StringComparison.Ordinal);
    }
}
