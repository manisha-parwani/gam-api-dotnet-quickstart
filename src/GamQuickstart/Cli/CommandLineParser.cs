using GamQuickstart.Paging;

namespace GamQuickstart.Cli;

/// <summary>
/// Turns command-line arguments into validated <see cref="CommandOptions"/>.
/// </summary>
/// <remarks>
/// Hand-written on purpose: it is short, has no dependencies and every rule is unit tested.
/// </remarks>
public static class CommandLineParser
{
    public const string NetworkCommand = "network";
    public const string AdUnitsCommand = "adunits";
    public const string CreateImageCreativeCommand = "create-image-creative";

    /// <summary>Longest creative name accepted. Ad Manager limits names to 255 characters.</summary>
    public const int MaxCreativeNameLength = 255;

    /// <summary>A sanity limit for creative width and height, mostly to catch typos.</summary>
    public const int MaxCreativeDimension = 10_000;

    private static readonly HashSet<string> NoOptions = [];

    public static ParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return ParseResult.Failure("No command given.");
        }

        var command = args[0];
        var rest = args.Skip(1).ToList();

        if (command is "help" or "--help" or "-h" || rest.Any(a => a is "--help" or "-h"))
        {
            return ParseResult.Success(new HelpOptions());
        }

        return command switch
        {
            NetworkCommand => ParseNetwork(rest),
            AdUnitsCommand => ParseAdUnits(rest),
            CreateImageCreativeCommand => ParseCreateImageCreative(rest),
            _ => ParseResult.Failure($"Unknown command '{command}'."),
        };
    }

    private static ParseResult ParseNetwork(IEnumerable<string> tokens)
    {
        var reader = new OptionReader(NetworkCommand, tokens, NoOptions, NoOptions);
        return reader.Errors.Count == 0 ? ParseResult.Success(new NetworkOptions()) : ParseResult.Failure(reader.Errors);
    }

    private static ParseResult ParseAdUnits(IEnumerable<string> tokens)
    {
        var reader = new OptionReader(AdUnitsCommand, tokens, valueOptions: new HashSet<string> { "--top", "--page-size" }, NoOptions);

        var top = reader.GetInt("--top", min: 1, max: int.MaxValue);
        var pageSize = reader.GetInt("--page-size", min: 1, max: Pager.MaxPageSize) ?? Pager.MaxPageSize;

        return reader.Errors.Count == 0
            ? ParseResult.Success(new AdUnitsOptions(top, pageSize))
            : ParseResult.Failure(reader.Errors);
    }

    private static ParseResult ParseCreateImageCreative(IEnumerable<string> tokens)
    {
        var reader = new OptionReader(
            CreateImageCreativeCommand,
            tokens,
            valueOptions: new HashSet<string> { "--advertiser-id", "--image-url", "--width", "--height", "--name", "--click-through-url" },
            flagOptions: new HashSet<string> { "--dry-run", "--execute" });

        var advertiserId = reader.GetPositiveLong("--advertiser-id", required: true);
        var imageUrl = reader.GetHttpUrl("--image-url", required: true);
        var width = reader.GetInt("--width", min: 1, max: MaxCreativeDimension, required: true);
        var height = reader.GetInt("--height", min: 1, max: MaxCreativeDimension, required: true);
        var name = reader.GetString("--name", MaxCreativeNameLength);
        var clickThroughUrl = reader.GetHttpUrl("--click-through-url");

        var execute = reader.HasFlag("--execute");
        if (execute && reader.HasFlag("--dry-run"))
        {
            reader.Errors.Add("Use either --dry-run or --execute, not both.");
        }

        if (reader.Errors.Count > 0)
        {
            return ParseResult.Failure(reader.Errors);
        }

        return ParseResult.Success(new CreateImageCreativeOptions(
            advertiserId!.Value,
            imageUrl!,
            width!.Value,
            height!.Value,
            name,
            clickThroughUrl,
            execute));
    }
}
