using GamQuickstart.Gateway;

namespace GamQuickstart.Commands;

/// <summary><c>network</c>: prints the network the credentials and network code point to.</summary>
public sealed class NetworkCommand(IAdManagerGateway gateway, TextWriter output)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var network = await gateway.GetCurrentNetworkAsync(cancellationToken);

        output.WriteLine($"Network code:  {network.NetworkCode}");
        output.WriteLine($"Display name:  {network.DisplayName}");
        output.WriteLine($"Time zone:     {network.TimeZone}");
        output.WriteLine($"Currency:      {network.CurrencyCode}");
        return ExitCodes.Success;
    }
}
