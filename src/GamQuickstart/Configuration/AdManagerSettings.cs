namespace GamQuickstart.Configuration;

/// <summary>Validated settings needed to call the Ad Manager API.</summary>
/// <param name="NetworkCode">The numeric code of your Ad Manager network.</param>
/// <param name="ServiceAccountKeyPath">Full path to the service account JSON key file.</param>
/// <param name="ApplicationName">A name for this app, sent to the API with every request.</param>
public sealed record AdManagerSettings(string NetworkCode, string ServiceAccountKeyPath, string ApplicationName);
