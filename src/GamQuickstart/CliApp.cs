using System.Diagnostics;
using GamQuickstart.Cli;
using GamQuickstart.Commands;
using GamQuickstart.Configuration;
using GamQuickstart.Gateway;
using GamQuickstart.Resilience;
using Microsoft.Extensions.Logging;

namespace GamQuickstart;

/// <summary>
/// Parses arguments, runs the chosen command and turns known errors into friendly messages and exit codes.
/// </summary>
/// <param name="gatewayFactory">Creates the gateway on first use, so help and dry runs need no settings.</param>
/// <param name="output">Command results (stdout).</param>
/// <param name="error">Error messages (stderr).</param>
public sealed class CliApp(
    Func<IAdManagerGateway> gatewayFactory,
    TextWriter output,
    TextWriter error,
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider)
{
    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parsed = CommandLineParser.Parse(args);
        if (!parsed.IsSuccess)
        {
            foreach (var message in parsed.Errors)
            {
                error.WriteLine($"Error: {message}");
            }

            error.WriteLine("Run with 'help' to see all commands and options.");
            return ExitCodes.UsageError;
        }

        try
        {
            switch (parsed.Options)
            {
                case NetworkOptions:
                    return await new NetworkCommand(gatewayFactory(), output).RunAsync(cancellationToken);

                case AdUnitsOptions options:
                    return await new AdUnitsCommand(gatewayFactory(), output, loggerFactory.CreateLogger<AdUnitsCommand>())
                        .RunAsync(options, cancellationToken);

                case CreateImageCreativeOptions options:
                    return await new CreateImageCreativeCommand(gatewayFactory, output, timeProvider)
                        .RunAsync(options, cancellationToken);

                case HelpOptions:
                    output.Write(Usage.Text);
                    return ExitCodes.Success;

                default:
                    throw new UnreachableException($"No handler for {parsed.Options?.GetType().Name}.");
            }
        }
        catch (SettingsException ex)
        {
            error.WriteLine("Configuration problem:");
            error.WriteLine(ex.Message);
            error.WriteLine("See 'Setup' in README.md.");
        }
        catch (TransientApiException ex)
        {
            error.WriteLine($"The Ad Manager API is temporarily unavailable and retrying did not help ({ex.Kind}): {ex.Message}");
        }
        catch (AdManagerRequestException ex)
        {
            error.WriteLine($"The Ad Manager request failed: {ex.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Cancelled.");
        }

        return ExitCodes.Error;
    }
}
