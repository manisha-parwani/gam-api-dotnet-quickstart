using GamQuickstart.Cli;

namespace GamQuickstart.Tests;

public class CommandLineParserTests
{
    private static ParseResult Parse(params string[] args) => CommandLineParser.Parse(args);

    private static T ParseOk<T>(params string[] args)
        where T : CommandOptions
    {
        var result = Parse(args);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors));
        return Assert.IsType<T>(result.Options);
    }

    private static IReadOnlyList<string> ParseErrors(params string[] args)
    {
        var result = Parse(args);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        return result.Errors;
    }

    private static readonly string[] ValidCreate =
    [
        "create-image-creative",
        "--advertiser-id", "4242",
        "--image-url", "https://example.com/banner.png",
        "--width", "300",
        "--height", "250",
    ];

    // ----- general -----

    [Fact]
    public void No_arguments_is_an_error() =>
        Assert.Equal(["No command given."], ParseErrors());

    [Fact]
    public void Unknown_command_is_an_error() =>
        Assert.Equal(["Unknown command 'orders'."], ParseErrors("orders"));

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("adunits", "--help")]
    [InlineData("create-image-creative", "-h")]
    public void Help_is_recognised_anywhere(params string[] args) =>
        ParseOk<HelpOptions>(args);

    // ----- network -----

    [Fact]
    public void Network_parses() => ParseOk<NetworkOptions>("network");

    [Fact]
    public void Network_rejects_options() =>
        Assert.Equal(["Unknown option --top for 'network'."], ParseErrors("network", "--top", "5"));

    // ----- adunits -----

    [Fact]
    public void Adunits_defaults_to_all_results_with_the_max_page_size()
    {
        var options = ParseOk<AdUnitsOptions>("adunits");

        Assert.Null(options.Top);
        Assert.Equal(500, options.PageSize);
    }

    [Theory]
    [InlineData("--top", "25")]
    [InlineData("--top=25")]
    public void Adunits_reads_top_in_both_styles(params string[] optionTokens)
    {
        var options = ParseOk<AdUnitsOptions>(["adunits", .. optionTokens]);

        Assert.Equal(25, options.Top);
    }

    [Fact]
    public void Adunits_reads_page_size() =>
        Assert.Equal(2, ParseOk<AdUnitsOptions>("adunits", "--page-size", "2").PageSize);

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("ten")]
    [InlineData("1.5")]
    public void Adunits_rejects_a_top_that_is_not_a_positive_whole_number(string value)
    {
        var error = Assert.Single(ParseErrors("adunits", "--top", value));

        Assert.StartsWith("--top must be a whole number of 1 or more", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Adunits_rejects_a_page_size_above_the_api_limit()
    {
        var error = Assert.Single(ParseErrors("adunits", "--page-size", "501"));

        Assert.StartsWith("--page-size must be a whole number from 1 to 500", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_value_is_an_error() =>
        Assert.Equal(["--top needs a value."], ParseErrors("adunits", "--top"));

    [Fact]
    public void A_missing_value_does_not_swallow_the_next_option() =>
        Assert.Equal(["--top needs a value."], ParseErrors("adunits", "--top", "--page-size", "10"));

    [Fact]
    public void A_repeated_option_is_an_error() =>
        Assert.Contains("--top was given more than once.", ParseErrors("adunits", "--top", "1", "--top", "2"));

    [Fact]
    public void A_stray_positional_argument_is_an_error() =>
        Assert.Contains("Unexpected argument '25'. Options start with '--'.", ParseErrors("adunits", "25"));

    // ----- create-image-creative -----

    [Fact]
    public void Create_parses_all_required_options_and_defaults_to_a_dry_run()
    {
        var options = ParseOk<CreateImageCreativeOptions>(ValidCreate);

        Assert.Equal(4242, options.AdvertiserId);
        Assert.Equal(new Uri("https://example.com/banner.png"), options.ImageUrl);
        Assert.Equal(300, options.Width);
        Assert.Equal(250, options.Height);
        Assert.Null(options.Name);
        Assert.Null(options.ClickThroughUrl);
        Assert.False(options.Execute);
    }

    [Fact]
    public void Create_accepts_an_explicit_dry_run() =>
        Assert.False(ParseOk<CreateImageCreativeOptions>([.. ValidCreate, "--dry-run"]).Execute);

    [Fact]
    public void Create_executes_only_with_the_execute_flag() =>
        Assert.True(ParseOk<CreateImageCreativeOptions>([.. ValidCreate, "--execute"]).Execute);

    [Fact]
    public void Create_rejects_dry_run_and_execute_together() =>
        Assert.Equal(
            ["Use either --dry-run or --execute, not both."],
            ParseErrors([.. ValidCreate, "--dry-run", "--execute"]));

    [Fact]
    public void Create_reads_the_optional_name_and_click_through_url()
    {
        var options = ParseOk<CreateImageCreativeOptions>(
            [.. ValidCreate, "--name", "  Autumn sale 300x250 ", "--click-through-url", "https://example.com/sale"]);

        Assert.Equal("Autumn sale 300x250", options.Name);
        Assert.Equal(new Uri("https://example.com/sale"), options.ClickThroughUrl);
    }

    [Fact]
    public void Create_lists_every_missing_required_option_at_once() =>
        Assert.Equal(
            ["--advertiser-id is required.", "--image-url is required.", "--width is required.", "--height is required."],
            ParseErrors("create-image-creative"));

    [Theory]
    [InlineData("ftp://example.com/banner.png")]
    [InlineData("/images/banner.png")]
    [InlineData("not a url")]
    public void Create_requires_an_absolute_http_image_url(string url)
    {
        string[] args = [.. ValidCreate];
        args[Array.IndexOf(args, "--image-url") + 1] = url;

        var error = Assert.Single(ParseErrors(args));

        Assert.StartsWith("--image-url must be an absolute http:// or https:// URL", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--width", "0")]
    [InlineData("--width", "10001")]
    [InlineData("--height", "-250")]
    [InlineData("--height", "abc")]
    public void Create_rejects_sizes_out_of_range(string option, string value)
    {
        string[] args = [.. ValidCreate];
        args[Array.IndexOf(args, option) + 1] = value;

        var error = Assert.Single(ParseErrors(args));

        Assert.StartsWith($"{option} must be a whole number from 1 to 10000", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("12ab")]
    public void Create_rejects_an_invalid_advertiser_id(string value)
    {
        string[] args = [.. ValidCreate];
        args[Array.IndexOf(args, "--advertiser-id") + 1] = value;

        var error = Assert.Single(ParseErrors(args));

        Assert.StartsWith("--advertiser-id must be a positive whole number", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_rejects_a_name_that_is_too_long() =>
        Assert.Equal(
            ["--name must be at most 255 characters."],
            ParseErrors([.. ValidCreate, "--name", new string('x', 256)]));

    [Fact]
    public void Create_rejects_a_value_on_a_flag() =>
        Assert.Contains("--execute is a flag and does not take a value.", ParseErrors([.. ValidCreate, "--execute=yes"]));
}
