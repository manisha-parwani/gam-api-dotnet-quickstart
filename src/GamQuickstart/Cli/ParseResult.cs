namespace GamQuickstart.Cli;

/// <summary>Either valid <see cref="CommandOptions"/> or a list of every problem found.</summary>
public sealed record ParseResult
{
    private ParseResult(CommandOptions? options, IReadOnlyList<string> errors)
    {
        Options = options;
        Errors = errors;
    }

    public CommandOptions? Options { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsSuccess => Options is not null;

    public static ParseResult Success(CommandOptions options) => new(options, []);

    public static ParseResult Failure(IReadOnlyList<string> errors) =>
        errors.Count > 0 ? new(null, errors) : throw new ArgumentException("At least one error is required.", nameof(errors));

    public static ParseResult Failure(string error) => Failure([error]);
}
