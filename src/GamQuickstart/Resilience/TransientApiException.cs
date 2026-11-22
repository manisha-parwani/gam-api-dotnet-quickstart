namespace GamQuickstart.Resilience;

/// <summary>What kind of temporary problem happened.</summary>
public enum TransientErrorKind
{
    /// <summary>Too many requests (for example <c>QuotaError.EXCEEDED_QUOTA</c>). The request was rejected.</summary>
    Quota,

    /// <summary>The server failed or was busy (<c>ServerError.SERVER_ERROR</c> or <c>SERVER_BUSY</c>).</summary>
    Server,

    /// <summary>A timeout or connection problem before a full response arrived.</summary>
    Network,
}

/// <summary>
/// A failure that may succeed if the same request is sent again a bit later.
/// </summary>
/// <remarks>
/// The real gateway translates Google client library errors into this type, so the retry logic
/// does not depend on SOAP or library types and can be unit tested.
/// </remarks>
public sealed class TransientApiException : Exception
{
    public TransientApiException()
        : this(TransientErrorKind.Server, "A transient Ad Manager API error occurred.")
    {
    }

    public TransientApiException(string message)
        : this(TransientErrorKind.Server, message)
    {
    }

    public TransientApiException(string message, Exception innerException)
        : this(TransientErrorKind.Server, message, innerException)
    {
    }

    public TransientApiException(TransientErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public TransientErrorKind Kind { get; }

    /// <summary>
    /// True when the API rejected the request before doing any work.
    /// Only then is it safe to retry a call that creates something: after a server error or
    /// a timeout, the object may already exist and a retry could create a duplicate.
    /// </summary>
    public bool RequestWasRejected => Kind == TransientErrorKind.Quota;
}
