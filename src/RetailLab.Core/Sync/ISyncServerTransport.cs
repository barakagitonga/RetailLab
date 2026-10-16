namespace RetailLab.Core;

/// <summary>
/// Transport-independent boundary for the read-only synchronization server.
/// </summary>
public interface ISyncServerTransport
{
    Task<PairResponse> PairAsync(
        PairRequest request,
        CancellationToken cancellationToken = default);

    Task<PullResponse> PullAsync(
        PullRequest request,
        CancellationToken cancellationToken = default);
}
