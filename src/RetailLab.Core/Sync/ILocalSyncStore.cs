namespace RetailLab.Core;

/// <summary>
/// Persistence boundary for client pairing state and atomic page application.
/// </summary>
public interface ILocalSyncStore
{
    Task<SyncCheckpoint?> GetCheckpointAsync(CancellationToken cancellationToken = default);

    Task<EligibilityReport> CheckEligibilityAsync(CancellationToken cancellationToken = default);

    Task<bool> HasPendingOutboxWorkAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingSyncOperation>> GetPendingOperationsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task ApplyUploadResultsAsync(
        IReadOnlyList<UploadOperationResult> results,
        CancellationToken cancellationToken = default);

    Task<ProductSyncState?> GetProductSyncStateAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task CaptureOperationAsync(
        ISyncProductOperation operation,
        CancellationToken cancellationToken = default);

    Task MarkPairedAsync(
        Guid deviceId,
        Guid serverInstanceId,
        DateTimeOffset pairedAtUtc,
        CancellationToken cancellationToken = default);

    Task ApplyPageAsync(
        IReadOnlyList<ServerChangeEntry> entries,
        long expectedCursor,
        CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record ProductSyncState(
    Guid ProductId,
    long ServerRevision,
    Guid? LatestPendingOperationId);
