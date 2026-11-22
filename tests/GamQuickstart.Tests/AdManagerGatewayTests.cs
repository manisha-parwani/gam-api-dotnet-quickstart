using System.Security.Cryptography;
using System.Text.Json;
using GamQuickstart.Configuration;
using GamQuickstart.Gateway;

namespace GamQuickstart.Tests;

/// <summary>
/// Offline checks of how the real gateway reads the key file. No API calls are made.
/// Key files are written to the test output folder and contain a freshly generated throwaway key.
/// </summary>
public sealed class AdManagerGatewayTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "test-keys", Guid.NewGuid().ToString("N"));
    private readonly HttpClient _httpClient = new();

    public AdManagerGatewayTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _httpClient.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private string WriteKeyFile(object content)
    {
        var path = Path.Combine(_folder, "key.json");
        File.WriteAllText(path, JsonSerializer.Serialize(content));
        return path;
    }

    private AdManagerGateway CreateGateway(string keyPath) =>
        new(new AdManagerSettings("1234", keyPath, "tests"), _httpClient);

    [Fact]
    public void Accepts_a_well_formed_service_account_key()
    {
        using var rsa = RSA.Create(2048);
        var keyPath = WriteKeyFile(new
        {
            type = "service_account",
            client_email = "quickstart@example.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
        });

        Assert.NotNull(CreateGateway(keyPath));
    }

    [Fact]
    public void Rejects_a_key_file_without_client_email()
    {
        var keyPath = WriteKeyFile(new { type = "service_account" });

        var ex = Assert.Throws<SettingsException>(() => CreateGateway(keyPath));

        Assert.Contains("client_email", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_private_key_that_is_not_valid_pem()
    {
        var keyPath = WriteKeyFile(new
        {
            type = "service_account",
            client_email = "quickstart@example.iam.gserviceaccount.com",
            private_key = "-----BEGIN PRIVATE KEY-----\nnot-a-key\n-----END PRIVATE KEY-----\n",
        });

        var ex = Assert.Throws<SettingsException>(() => CreateGateway(keyPath));

        Assert.Contains("not a valid PEM private key", ex.Message, StringComparison.Ordinal);
    }
}
