using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Durable local outbox entry capturing a staff operation performed offline.
/// Ordered by database-generated LocalSequence for deterministic FIFO processing.
/// Business fields are immutable after creation; state transitions are recorded.
/// </summary>
public sealed class SyncOutboxEntry
{
    private SyncOutboxEntry()
    {
    }

    public SyncOutboxEntry(
        Guid operationId,
        Guid productId,
        SyncOperationKind kind,
        string payload,
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

        ArgumentException.ThrowIfNullOrWhiteSpace(payload, nameof(payload));

        if (kind == SyncOperationKind.CreateProduct)
        {
            if (baseServerRevision.HasValue || predecessorOperationId.HasValue)
            {
                throw new BusinessRuleException(
                    "CreateProduct operations cannot have a base server revision or predecessor operation ID.");
            }
        }
        else
        {
            var hasBase = baseServerRevision.HasValue;
            var hasPred = predecessorOperationId.HasValue && predecessorOperationId.Value != Guid.Empty;

            if (predecessorOperationId == Guid.Empty)
            {
                throw new ArgumentException("Predecessor operation ID cannot be empty.", nameof(predecessorOperationId));
            }

            if (hasBase && baseServerRevision!.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseServerRevision),
                    "Base server revision cannot be negative.");
            }

            if (hasBase == hasPred)
            {
                throw new BusinessRuleException(
                    "Mutating operations must have either a base server revision or a predecessor operation ID, but not both.");
            }
        }

        OperationId = operationId;
        ProductId = productId;
        Kind = kind;
        Payload = payload;
        BaseServerRevision = baseServerRevision;
        PredecessorOperationId = predecessorOperationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        State = OutboxProcessingState.Pending;
        RecordedResult = null;
    }

    public long LocalSequence { get; private set; }

    public Guid OperationId { get; private set; }

    public Guid ProductId { get; private set; }

    public SyncOperationKind Kind { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public long? BaseServerRevision { get; private set; }

    public Guid? PredecessorOperationId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public OutboxProcessingState State { get; private set; }

    public string? RecordedResult { get; private set; }

    public void MarkProcessed(string? recordedResult = null)
    {
        State = OutboxProcessingState.Processed;
        RecordedResult = recordedResult;
    }

    public void MarkConflicted(string? recordedResult)
    {
        State = OutboxProcessingState.Conflicted;
        RecordedResult = recordedResult;
    }

    public void MarkRejected(string? recordedResult)
    {
        State = OutboxProcessingState.Rejected;
        RecordedResult = recordedResult;
    }
}
