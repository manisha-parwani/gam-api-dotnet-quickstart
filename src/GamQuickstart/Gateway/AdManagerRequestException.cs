namespace GamQuickstart.Gateway;

/// <summary>
/// The request failed and sending it again will not help, for example because of invalid input,
/// a missing permission, failed authentication or an image that could not be downloaded.
/// </summary>
public sealed class AdManagerRequestException : Exception
{
    public AdManagerRequestException()
        : base("The Ad Manager request failed.")
    {
    }

    public AdManagerRequestException(string message)
        : base(message)
    {
    }

    public AdManagerRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
