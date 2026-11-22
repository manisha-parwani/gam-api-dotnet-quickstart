namespace GamQuickstart.Cli;

/// <summary>The help text.</summary>
public static class Usage
{
    public const string Text = """
        gam-api-dotnet-quickstart: call the Google Ad Manager SOAP API from .NET.

        Usage:
          dotnet run --project src/GamQuickstart -- <command> [options]

        Commands:
          network                        Show the current network: code, name, time zone, currency.

          adunits                        List ad units, page by page.
            --top <N>                    Stop after N ad units.
            --page-size <N>              Ad units per API call, 1 to 500 (default 500).

          create-image-creative          Create an image creative. This is a DRY RUN unless you add --execute.
            --advertiser-id <ID>         Required. ID of the advertiser company that owns the creative.
            --image-url <URL>            Required. http(s) URL of the image to upload.
            --width <W>                  Required. Width in pixels.
            --height <H>                 Required. Height in pixels.
            --name <NAME>                Creative name (default: "Quickstart image WxH <UTC time>").
            --click-through-url <URL>    Landing page for clicks (optional).
            --dry-run                    Only print what would be created (this is the default).
            --execute                    Really create the creative.

          help                           Show this help.

        Configuration (environment variables win over appsettings.json):
          GAM_NETWORK_CODE               Your Ad Manager network code.
          GAM_SERVICE_ACCOUNT_KEY_PATH   Path to the service account JSON key. Keep it outside the repository.
          GAM_APPLICATION_NAME           Optional. Application name sent to the API.

        """;
}
