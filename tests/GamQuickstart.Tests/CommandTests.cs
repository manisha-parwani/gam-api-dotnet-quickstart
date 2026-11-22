using GamQuickstart.Cli;
using GamQuickstart.Commands;
using GamQuickstart.Gateway;
using GamQuickstart.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamQuickstart.Tests;

public class NetworkCommandTests
{
    [Fact]
    public async Task Prints_the_network_fields()
    {
        var gateway = new FakeAdManagerGateway { Network = new("21700000", "Demo network", "Europe/Helsinki", "EUR") };
        var output = new StringWriter();

        var exitCode = await new NetworkCommand(gateway, output).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        var text = output.ToString();
        Assert.Contains("Network code:  21700000", text, StringComparison.Ordinal);
        Assert.Contains("Display name:  Demo network", text, StringComparison.Ordinal);
        Assert.Contains("Time zone:     Europe/Helsinki", text, StringComparison.Ordinal);
        Assert.Contains("Currency:      EUR", text, StringComparison.Ordinal);
    }
}

public class AdUnitsCommandTests
{
    private readonly FakeAdManagerGateway _gateway = new();
    private readonly StringWriter _output = new();

    private Task<int> RunAsync(int? top, int pageSize) =>
        new AdUnitsCommand(_gateway, _output, NullLogger<AdUnitsCommand>.Instance)
            .RunAsync(new AdUnitsOptions(top, pageSize), TestContext.Current.CancellationToken);

    private string[] Lines() => _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public async Task Lists_every_ad_unit_across_pages()
    {
        _gateway.AdUnits.AddRange(FakeAdManagerGateway.MakeAdUnits(5));

        var exitCode = await RunAsync(top: null, pageSize: 2);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal([(0, 2), (2, 2), (4, 2)], _gateway.AdUnitPageCalls);
        var lines = Lines();
        Assert.StartsWith("ID", lines[0], StringComparison.Ordinal);
        Assert.Equal(5, lines.Count(l => l.Contains("ACTIVE", StringComparison.Ordinal)));
        Assert.Equal("Listed 5 of 5 ad unit(s) using 3 API call(s).", lines[^1]);
    }

    [Fact]
    public async Task Stops_after_top_ad_units()
    {
        _gateway.AdUnits.AddRange(FakeAdManagerGateway.MakeAdUnits(10));

        await RunAsync(top: 3, pageSize: 500);

        Assert.Equal([(0, 3)], _gateway.AdUnitPageCalls);
        Assert.Equal("Listed 3 of 10 ad unit(s) using 1 API call(s).", Lines()[^1]);
    }

    [Fact]
    public async Task Says_so_when_there_are_no_ad_units()
    {
        await RunAsync(top: null, pageSize: 500);

        Assert.Equal(["No ad units found."], Lines());
    }
}

public class CreateImageCreativeCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly FakeAdManagerGateway _gateway = new();
    private readonly StringWriter _output = new();
    private int _gatewaysCreated;

    private CreateImageCreativeCommand CreateCommand() =>
        new(
            () =>
            {
                _gatewaysCreated++;
                return _gateway;
            },
            _output,
            new FixedTimeProvider(Now));

    private static CreateImageCreativeOptions Options(bool execute, string? name = null, Uri? clickThroughUrl = null) =>
        new(4242, new Uri("https://example.com/banner.png"), 300, 250, name, clickThroughUrl, execute);

    [Fact]
    public async Task Dry_run_prints_the_plan_and_never_creates_a_gateway()
    {
        var exitCode = await CreateCommand().RunAsync(Options(execute: false), TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(0, _gatewaysCreated);
        Assert.Empty(_gateway.CreatedRequests);

        var text = _output.ToString();
        Assert.StartsWith("DRY RUN: nothing was sent to Ad Manager.", text, StringComparison.Ordinal);
        Assert.Contains("Advertiser ID:     4242", text, StringComparison.Ordinal);
        Assert.Contains("Size:              300x250", text, StringComparison.Ordinal);
        Assert.Contains("Image URL:         https://example.com/banner.png", text, StringComparison.Ordinal);
        Assert.Contains("Click-through URL: (none: clicks do nothing)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_sends_exactly_one_create_request_and_prints_the_new_id()
    {
        var exitCode = await CreateCommand().RunAsync(
            Options(execute: true, name: "Autumn sale", clickThroughUrl: new Uri("https://example.com/sale")),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        var request = Assert.Single(_gateway.CreatedRequests);
        Assert.Equal(
            new ImageCreativeRequest(4242, "Autumn sale", new Uri("https://example.com/banner.png"), 300, 250, new Uri("https://example.com/sale")),
            request);
        Assert.Contains("Created image creative with ID 9001 and name \"Autumn sale\".", _output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DRY RUN", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_default_name_is_generated_from_the_size_and_the_utc_time()
    {
        var request = CreateCommand().BuildRequest(Options(execute: false));

        Assert.Equal("Quickstart image 300x250 2026-11-22 09:30:00 UTC", request.Name);
    }
}
