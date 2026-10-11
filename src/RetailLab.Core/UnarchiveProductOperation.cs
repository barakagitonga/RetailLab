namespace RetailLab.Core;

/// <summary>
/// Synchronization contract representing a client-side product unarchival.
/// Must specify either BaseServerRevision or PredecessorOperationId, but not both.
/// </summary>
public sealed class UnarchiveProductOperation : ICausalOperation
{
    public UnarchiveProductOperation(
        Guid operationId,
        Guid productId,
        long? baseServerRevision,
        Guid? predecessorOperationId,
        DateTimeOffset createdAtUtc)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        CausalChainValidator.Validate(baseServerRevision, predecessorOperationId);

        OperationId = operationId;
        ProductId = productId;
        BaseServerRevision = baseServerRevision;
        PredecessorOperationId = predecessorOperationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid OperationId { get; }

    public Guid ProductId { get; }

    public SyncOperationKind Kind => SyncOperationKind.UnarchiveProduct;

    public long? BaseServerRevision { get; }

    public Guid? PredecessorOperationId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static UnarchiveProductOperation BasedOnServer(
        Guid operationId,
        Guid productId,
        long baseServerRevision,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, baseServerRevision, null, createdAtUtc);

    public static UnarchiveProductOperation Chained(
        Guid operationId,
        Guid productId,
        Guid predecessorOperationId,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, null, predecessorOperationId, createdAtUtc);
}
