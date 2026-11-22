using GamQuickstart.Cli;
using GamQuickstart.Gateway;
using GamQuickstart.Paging;
using Microsoft.Extensions.Logging;

namespace GamQuickstart.Commands;

/// <summary><c>adunits</c>: lists ad units using limit/offset paging.</summary>
public sealed partial class AdUnitsCommand(IAdManagerGateway gateway, TextWriter output, ILogger<AdUnitsCommand> logger)
{
    public async Task<int> RunAsync(AdUnitsOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var apiCalls = 0;
        var total = 0;

        async Task<Page<AdUnitSummary>> FetchPageAsync(int offset, int limit, CancellationToken ct)
        {
            var page = await gateway.GetAdUnitsPageAsync(offset, limit, ct);
            apiCalls++;
            total = page.TotalResultSetSize;
            LogPage(apiCalls, offset, limit, page.Items.Count, page.TotalResultSetSize);
            return page;
        }

        var listed = 0;
        await foreach (var adUnit in Pager.ReadAllAsync(FetchPageAsync, options.PageSize, options.Top, cancellationToken))
        {
            if (listed == 0)
            {
                output.WriteLine($"{"ID",-14} {"STATUS",-10} NAME");
            }

            output.WriteLine($"{adUnit.Id,-14} {adUnit.Status,-10} {adUnit.Name}");
            listed++;
        }

        output.WriteLine(listed == 0
            ? "No ad units found."
            : $"Listed {listed} of {total} ad unit(s) using {apiCalls} API call(s).");
        return ExitCodes.Success;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Page {Page}: asked for {Limit} ad units at offset {Offset}, got {Count} ({Total} in total).")]
    private partial void LogPage(int page, int offset, int limit, int count, int total);
}
