namespace RetailLab.Core;

/// <summary>
/// Synchronization contract representing a signed stock adjustment captured by an offline device.
/// The delta is rebased over the current server quantity when the server accepts the operation.
/// </summary>
public sealed class AdjustStockOperation : ICausalOperation
{
    public AdjustStockOperation(
        Guid operationId,
        Guid productId,
        int delta,
        string reason,
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

        if (delta == 0)
        {
            throw new BusinessRuleException("Stock adjustment must not be zero.");
        }

        SynchronizedAdjustmentReasons.Validate(reason, delta);
        CausalChainValidator.Validate(baseServerRevision, predecessorOperationId);

        OperationId = operationId;
        ProductId = productId;
        Delta = delta;
        Reason = reason;
        BaseServerRevision = baseServerRevision;
        PredecessorOperationId = predecessorOperationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid OperationId { get; }

    public Guid ProductId { get; }

    public SyncOperationKind Kind => SyncOperationKind.AdjustStock;

    public int Delta { get; }

    public string Reason { get; }

    public long? BaseServerRevision { get; }

    public Guid? PredecessorOperationId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static AdjustStockOperation BasedOnServer(
        Guid operationId,
        Guid productId,
        int delta,
        string reason,
        long baseServerRevision,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, delta, reason, baseServerRevision, null, createdAtUtc);

    public static AdjustStockOperation Chained(
        Guid operationId,
        Guid productId,
        int delta,
        string reason,
        Guid predecessorOperationId,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, delta, reason, null, predecessorOperationId, createdAtUtc);
}
