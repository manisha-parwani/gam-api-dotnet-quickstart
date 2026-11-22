using GamQuickstart;
using GamQuickstart.Configuration;
using GamQuickstart.Gateway;
using GamQuickstart.Resilience;
using Microsoft.Extensions.Logging;

// Composition root: build the real dependencies and hand them to CliApp.

using var loggerFactory = LoggerFactory.Create(logging => logging
    // Logs go to stderr so stdout only has command output (handy for piping).
    .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        // Quoted colons, so the time looks the same in every culture.
        options.TimestampFormat = "HH':'mm':'ss ";
    }));

// Used to download the creative image. The buffer limit stops very large downloads.
using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30),
    MaxResponseContentBufferSize = 10 * 1024 * 1024,
};

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

IAdManagerGateway CreateGateway()
{
    var configuration = SettingsLoader.BuildConfiguration(AppContext.BaseDirectory);
    var settings = SettingsLoader.Load(configuration, Environment.GetEnvironmentVariable, File.Exists);
    var retryPolicy = new RetryPolicy(new RetryOptions(), loggerFactory.CreateLogger<RetryPolicy>());

    return new RetryingAdManagerGateway(new AdManagerGateway(settings, httpClient), retryPolicy);
}

var app = new CliApp(CreateGateway, Console.Out, Console.Error, loggerFactory, TimeProvider.System);
return await app.RunAsync(args, cancellation.Token);
