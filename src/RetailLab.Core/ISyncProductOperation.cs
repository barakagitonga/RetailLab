namespace RetailLab.Core;

/// <summary>
/// Common contract for all synchronization operations targeting a product.
/// </summary>
public interface ISyncProductOperation
{
    Guid OperationId { get; }

    Guid ProductId { get; }

    SyncOperationKind Kind { get; }

    DateTimeOffset CreatedAtUtc { get; }
}
