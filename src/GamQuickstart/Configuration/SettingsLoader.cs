using Microsoft.Extensions.Configuration;

namespace GamQuickstart.Configuration;

/// <summary>
/// Reads <see cref="AdManagerSettings"/> from environment variables and an optional <c>appsettings.json</c>.
/// Environment variables win, so CI or a shell profile can override the file.
/// </summary>
public static class SettingsLoader
{
    public const string SectionName = "AdManager";
    public const string NetworkCodeVariable = "GAM_NETWORK_CODE";
    public const string KeyPathVariable = "GAM_SERVICE_ACCOUNT_KEY_PATH";
    public const string ApplicationNameVariable = "GAM_APPLICATION_NAME";
    public const string DefaultApplicationName = "gam-api-dotnet-quickstart";

    /// <summary>Builds configuration from <c>appsettings.json</c> in <paramref name="baseDirectory"/>, if it exists.</summary>
    public static IConfiguration BuildConfiguration(string baseDirectory) =>
        new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

    /// <summary>Reads and validates the settings. Throws <see cref="SettingsException"/> listing every problem.</summary>
    /// <param name="configuration">Usually the result of <see cref="BuildConfiguration"/>.</param>
    /// <param name="getEnvironmentVariable">Usually <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <param name="fileExists">Usually <see cref="File.Exists(string)"/>.</param>
    public static AdManagerSettings Load(
        IConfiguration configuration,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(fileExists);

        var section = configuration.GetSection(SectionName);
        var networkCode = FirstValue(getEnvironmentVariable(NetworkCodeVariable), section["NetworkCode"]);
        var keyPath = FirstValue(getEnvironmentVariable(KeyPathVariable), section["ServiceAccountKeyPath"]);
        var applicationName = FirstValue(getEnvironmentVariable(ApplicationNameVariable), section["ApplicationName"])
            ?? DefaultApplicationName;

        var problems = new List<string>();

        if (networkCode is null)
        {
            problems.Add($"The network code is missing. Set {NetworkCodeVariable} or {SectionName}:NetworkCode in appsettings.json.");
        }
        else if (!networkCode.All(char.IsAsciiDigit))
        {
            problems.Add(
                $"The network code must contain only digits, but it is '{networkCode}'. "
                + "You can find it in the Ad Manager URL: https://admanager.google.com/<network code>.");
        }

        if (keyPath is null)
        {
            problems.Add(
                $"The service account key path is missing. Set {KeyPathVariable} or {SectionName}:ServiceAccountKeyPath in appsettings.json.");
        }
        else
        {
            keyPath = ExpandHomeDirectory(keyPath);
            if (!fileExists(keyPath))
            {
                problems.Add($"The service account key file was not found: {keyPath}");
            }
        }

        if (problems.Count > 0)
        {
            throw new SettingsException(string.Join(Environment.NewLine, problems.Select(p => $"- {p}")));
        }

        return new AdManagerSettings(networkCode!, keyPath!, applicationName);
    }

    private static string? FirstValue(params string?[] candidates) =>
        candidates.Select(c => c?.Trim()).FirstOrDefault(c => !string.IsNullOrEmpty(c));

    /// <summary>Turns <c>~/keys/key.json</c> into a full path, like a shell would.</summary>
    private static string ExpandHomeDirectory(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/'))
            : path;
}
