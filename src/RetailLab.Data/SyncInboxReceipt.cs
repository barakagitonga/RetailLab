using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Server-side idempotency receipt keyed by OperationId.
/// Durably records the terminal outcome (Accepted, Conflict, or Rejected) and recorded result.
/// </summary>
public sealed class SyncInboxReceipt
{
    private SyncInboxReceipt()
    {
    }

    public SyncInboxReceipt(
        Guid operationId,
        SyncOperationOutcome outcome,
        string? recordedResult,
        DateTimeOffset processedAtUtc)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        OperationId = operationId;
        Outcome = outcome;
        RecordedResult = recordedResult;
        ProcessedAtUtc = processedAtUtc.ToUniversalTime();
    }

    public Guid OperationId { get; private set; }

    public SyncOperationOutcome Outcome { get; private set; }

    public string? RecordedResult { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
