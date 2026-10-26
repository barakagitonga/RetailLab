using RetailLab.Core;

namespace RetailLab.Sync;

/// <summary>
/// Builds immutable Core operations for offline staff actions. The store owns
/// the atomic local-mutation-plus-outbox transaction behind the interface.
/// </summary>
public sealed class OfflineProductOperationService(
    ILocalSyncStore store,
    TimeProvider timeProvider)
{
    public async Task<CreateProductOperation> CreateAsync(
        string sku,
        string description,
        decimal price,
        int initialStock,
        CancellationToken cancellationToken = default)
    {
        var operation = new CreateProductOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sku,
            description,
            price,
            initialStock,
            timeProvider.GetUtcNow());
        await store.CaptureOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<UpdateProductDetailsOperation> UpdateAsync(
        Guid productId,
        string description,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var state = await RequireStateAsync(productId, cancellationToken);
        var operation = new UpdateProductDetailsOperation(
            Guid.NewGuid(),
            productId,
            description,
            price,
            state.LatestPendingOperationId.HasValue ? null : state.ServerRevision,
            state.LatestPendingOperationId,
            timeProvider.GetUtcNow());
        await store.CaptureOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<ArchiveProductOperation> ArchiveAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var state = await RequireStateAsync(productId, cancellationToken);
        var operation = new ArchiveProductOperation(
            Guid.NewGuid(),
            productId,
            state.LatestPendingOperationId.HasValue ? null : state.ServerRevision,
            state.LatestPendingOperationId,
            timeProvider.GetUtcNow());
        await store.CaptureOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<UnarchiveProductOperation> UnarchiveAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var state = await RequireStateAsync(productId, cancellationToken);
        var operation = new UnarchiveProductOperation(
            Guid.NewGuid(),
            productId,
            state.LatestPendingOperationId.HasValue ? null : state.ServerRevision,
            state.LatestPendingOperationId,
            timeProvider.GetUtcNow());
        await store.CaptureOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<AdjustStockOperation> AdjustStockAsync(
        Guid productId,
        int delta,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var state = await RequireStateAsync(productId, cancellationToken);
        var operation = new AdjustStockOperation(
            Guid.NewGuid(),
            productId,
            delta,
            reason,
            state.LatestPendingOperationId.HasValue ? null : state.ServerRevision,
            state.LatestPendingOperationId,
            timeProvider.GetUtcNow());
        await store.CaptureOperationAsync(operation, cancellationToken);
        return operation;
    }

    private async Task<ProductSyncState> RequireStateAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        return await store.GetProductSyncStateAsync(productId, cancellationToken)
            ?? throw new BusinessRuleException($"Product {productId:D} was not found locally.");
    }
}
