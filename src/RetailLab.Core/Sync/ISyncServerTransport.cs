namespace RetailLab.Core;

/// <summary>
/// Transport-independent boundary for the synchronization server.
/// </summary>
public interface ISyncServerTransport
{
    Task<PairResponse> PairAsync(
        PairRequest request,
        CancellationToken cancellationToken = default);

    Task<PullResponse> PullAsync(
        PullRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadResponse> UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken = default);
}
