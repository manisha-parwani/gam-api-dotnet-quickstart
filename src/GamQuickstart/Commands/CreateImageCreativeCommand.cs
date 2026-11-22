using System.Globalization;
using GamQuickstart.Cli;
using GamQuickstart.Gateway;

namespace GamQuickstart.Commands;

/// <summary>
/// <c>create-image-creative</c>: creates one image creative, or (by default) only shows what it would create.
/// </summary>
/// <remarks>
/// The gateway is created lazily through a factory, so a dry run needs no credentials and makes no network calls.
/// </remarks>
public sealed class CreateImageCreativeCommand(
    Func<IAdManagerGateway> gatewayFactory,
    TextWriter output,
    TimeProvider timeProvider)
{
    public async Task<int> RunAsync(CreateImageCreativeOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var request = BuildRequest(options);

        if (!options.Execute)
        {
            output.WriteLine("DRY RUN: nothing was sent to Ad Manager. Add --execute to create this creative.");
            output.WriteLine();
            output.WriteLine("Would create an ImageCreative:");
            WriteRequest(request);
            return ExitCodes.Success;
        }

        var gateway = gatewayFactory();
        output.WriteLine("Creating an ImageCreative:");
        WriteRequest(request);

        var created = await gateway.CreateImageCreativeAsync(request, cancellationToken);

        output.WriteLine();
        output.WriteLine($"Created image creative with ID {created.Id} and name \"{created.Name}\".");
        if (!string.IsNullOrEmpty(created.PreviewUrl))
        {
            output.WriteLine($"Preview URL: {created.PreviewUrl}");
        }

        return ExitCodes.Success;
    }

    /// <summary>Turns parsed options into a request, filling in a default name if none was given.</summary>
    public ImageCreativeRequest BuildRequest(CreateImageCreativeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var name = options.Name ?? string.Create(
            CultureInfo.InvariantCulture,
            $"Quickstart image {options.Width}x{options.Height} {timeProvider.GetUtcNow():yyyy-MM-dd HH:mm:ss} UTC");

        return new ImageCreativeRequest(
            options.AdvertiserId,
            name,
            options.ImageUrl,
            options.Width,
            options.Height,
            options.ClickThroughUrl);
    }

    private void WriteRequest(ImageCreativeRequest request)
    {
        output.WriteLine($"  Name:              {request.Name}");
        output.WriteLine($"  Advertiser ID:     {request.AdvertiserId}");
        output.WriteLine($"  Size:              {request.Width}x{request.Height}");
        output.WriteLine($"  Image URL:         {request.ImageUrl}");
        output.WriteLine($"  Click-through URL: {request.ClickThroughUrl?.ToString() ?? "(none: clicks do nothing)"}");
    }
}
