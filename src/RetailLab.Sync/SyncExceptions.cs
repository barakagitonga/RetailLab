namespace RetailLab.Sync;

public abstract class SyncException : Exception
{
    protected SyncException(string message)
        : base(message)
    {
    }

    protected SyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class SyncConfigurationException : SyncException
{
    public SyncConfigurationException(string message) : base(message) { }
}

public sealed class SyncAuthenticationException : SyncException
{
    public SyncAuthenticationException()
        : base("The server rejected the read key. Check RETAILLAB_SYNC_READ_KEY.") { }
}

public sealed class SyncServerUnavailableException : SyncException
{
    public SyncServerUnavailableException(Uri serverUrl, Exception innerException)
        : base($"Cannot reach the server at {serverUrl}. Is RetailLab.Web running?", innerException) { }
}

public sealed class SyncTimeoutException : SyncException
{
    public SyncTimeoutException(TimeSpan timeout, Exception innerException)
        : base($"The server did not respond within {timeout.TotalSeconds:0} seconds.", innerException) { }
}

public sealed class SyncServerResponseException : SyncException
{
    public SyncServerResponseException(int statusCode, string? serverCode)
        : base(serverCode is null
            ? $"The server returned an unexpected error (HTTP {statusCode})."
            : $"The server returned an unexpected error (HTTP {statusCode}, code {serverCode}).")
    {
        StatusCode = statusCode;
        ServerCode = serverCode;
    }

    public int StatusCode { get; }

    public string? ServerCode { get; }
}

public sealed class SyncProtocolException : SyncException
{
    public SyncProtocolException(string message) : base(message) { }

    public SyncProtocolException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ServerIdentityChangedException : SyncException
{
    public ServerIdentityChangedException()
        : base("The server identity changed. No page from the mismatched server was applied. Sync stopped.") { }
}

public sealed class CursorBeyondHeadException : SyncException
{
    public CursorBeyondHeadException(long requestedCursor, long? headSequence)
        : base(headSequence.HasValue
            ? $"The local cursor {requestedCursor} is beyond the server head {headSequence.Value}. The cursor was not moved."
            : $"The local cursor {requestedCursor} is beyond the server head. The cursor was not moved.")
    {
        RequestedCursor = requestedCursor;
        HeadSequence = headSequence;
    }

    public long RequestedCursor { get; }

    public long? HeadSequence { get; }
}

public sealed class SyncFeedGapException : SyncException
{
    public SyncFeedGapException(long? expectedSequence = null, long? actualSequence = null)
        : base(expectedSequence.HasValue && actualSequence.HasValue
            ? $"The feed contains a gap: expected sequence {expectedSequence.Value}, but received {actualSequence.Value}."
            : "The server reported a gap in the authoritative feed.")
    {
        ExpectedSequence = expectedSequence;
        ActualSequence = actualSequence;
    }

    public long? ExpectedSequence { get; }

    public long? ActualSequence { get; }
}

public sealed class DatabaseNotEligibleForPairingException : SyncException
{
    public DatabaseNotEligibleForPairingException(long productCount, long outboxEntryCount)
        : base("The local database is not empty and cannot be paired safely.")
    {
        ProductCount = productCount;
        OutboxEntryCount = outboxEntryCount;
    }

    public long ProductCount { get; }

    public long OutboxEntryCount { get; }
}
