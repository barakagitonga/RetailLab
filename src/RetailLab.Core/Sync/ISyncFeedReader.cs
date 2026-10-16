namespace RetailLab.Core;

/// <summary>
/// Storage-independent read boundary for the authoritative server change feed.
/// HTTP endpoints depend on this contract rather than on EF Core entities.
/// </summary>
public interface ISyncFeedReader
{
    Task<long> GetHeadSequenceAsync(CancellationToken cancellationToken = default);

    Task<PullResponse> ReadAfterAsync(
        long afterSequence,
        int limit,
        CancellationToken cancellationToken = default);
}
