namespace GamQuickstart.Cli;

/// <summary>The validated options of one command. Produced by <see cref="CommandLineParser"/>.</summary>
public abstract record CommandOptions;

/// <summary><c>help</c>: print usage.</summary>
public sealed record HelpOptions : CommandOptions;

/// <summary><c>network</c>: print the current network.</summary>
public sealed record NetworkOptions : CommandOptions;

/// <summary><c>adunits</c>: list ad units page by page.</summary>
/// <param name="Top">Stop after this many ad units. Null means list all.</param>
/// <param name="PageSize">Ad units per API call.</param>
public sealed record AdUnitsOptions(int? Top, int PageSize) : CommandOptions;

/// <summary><c>create-image-creative</c>: create (or dry run) one image creative.</summary>
/// <param name="Name">Creative name. Null means "use a generated name".</param>
/// <param name="Execute">False (the default) means dry run: print only, call nothing.</param>
public sealed record CreateImageCreativeOptions(
    long AdvertiserId,
    Uri ImageUrl,
    int Width,
    int Height,
    string? Name,
    Uri? ClickThroughUrl,
    bool Execute) : CommandOptions;
