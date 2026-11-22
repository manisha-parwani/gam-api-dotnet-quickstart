using GamQuickstart.Configuration;
using Microsoft.Extensions.Configuration;

namespace GamQuickstart.Tests;

public class SettingsLoaderTests
{
    private readonly Dictionary<string, string?> _json = [];
    private readonly Dictionary<string, string?> _environment = [];
    private readonly HashSet<string> _existingFiles = ["/keys/key.json", "/keys/other.json"];

    private AdManagerSettings Load() =>
        SettingsLoader.Load(
            new ConfigurationBuilder().AddInMemoryCollection(_json).Build(),
            name => _environment.GetValueOrDefault(name),
            _existingFiles.Contains);

    [Fact]
    public void Reads_settings_from_the_json_file()
    {
        _json["AdManager:NetworkCode"] = "1234";
        _json["AdManager:ServiceAccountKeyPath"] = "/keys/key.json";
        _json["AdManager:ApplicationName"] = "my-app";

        Assert.Equal(new AdManagerSettings("1234", "/keys/key.json", "my-app"), Load());
    }

    [Fact]
    public void Environment_variables_win_over_the_json_file()
    {
        _json["AdManager:NetworkCode"] = "1234";
        _json["AdManager:ServiceAccountKeyPath"] = "/keys/key.json";
        _environment["GAM_NETWORK_CODE"] = "5678";
        _environment["GAM_SERVICE_ACCOUNT_KEY_PATH"] = "/keys/other.json";
        _environment["GAM_APPLICATION_NAME"] = "from-env";

        Assert.Equal(new AdManagerSettings("5678", "/keys/other.json", "from-env"), Load());
    }

    [Fact]
    public void Application_name_has_a_default()
    {
        _environment["GAM_NETWORK_CODE"] = "1234";
        _environment["GAM_SERVICE_ACCOUNT_KEY_PATH"] = "/keys/key.json";

        Assert.Equal("gam-api-dotnet-quickstart", Load().ApplicationName);
    }

    [Fact]
    public void Blank_environment_variables_are_ignored()
    {
        _json["AdManager:NetworkCode"] = "1234";
        _json["AdManager:ServiceAccountKeyPath"] = "/keys/key.json";
        _environment["GAM_NETWORK_CODE"] = "   ";

        Assert.Equal("1234", Load().NetworkCode);
    }

    [Fact]
    public void Lists_every_missing_setting_and_how_to_set_it()
    {
        var ex = Assert.Throws<SettingsException>(Load);

        Assert.Contains("network code is missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("GAM_NETWORK_CODE", ex.Message, StringComparison.Ordinal);
        Assert.Contains("service account key path is missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("GAM_SERVICE_ACCOUNT_KEY_PATH", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("INSERT_NETWORK_CODE_HERE")]
    [InlineData("12-34")]
    public void Network_code_must_be_digits_only(string networkCode)
    {
        _environment["GAM_NETWORK_CODE"] = networkCode;
        _environment["GAM_SERVICE_ACCOUNT_KEY_PATH"] = "/keys/key.json";

        var ex = Assert.Throws<SettingsException>(Load);

        Assert.Contains("must contain only digits", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_file_must_exist()
    {
        _environment["GAM_NETWORK_CODE"] = "1234";
        _environment["GAM_SERVICE_ACCOUNT_KEY_PATH"] = "/keys/missing.json";

        var ex = Assert.Throws<SettingsException>(Load);

        Assert.Contains("key file was not found: /keys/missing.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tilde_in_the_key_path_means_the_home_directory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(home, ".config/gam/key.json");
        _existingFiles.Add(expected);
        _environment["GAM_NETWORK_CODE"] = "1234";
        _environment["GAM_SERVICE_ACCOUNT_KEY_PATH"] = "~/.config/gam/key.json";

        Assert.Equal(expected, Load().ServiceAccountKeyPath);
    }
}
