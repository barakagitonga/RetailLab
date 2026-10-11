namespace RetailLab.Core;

/// <summary>
/// Result of evaluating an uploaded synchronization operation on the server.
/// </summary>
public sealed record SyncOperationResult
{
    public SyncOperationResult(
        Guid operationId,
        SyncOperationOutcome outcome,
        long? resultingServerRevision = null,
        string? message = null,
        ProductSnapshot? conflictingSnapshot = null)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        if (resultingServerRevision.HasValue && resultingServerRevision.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultingServerRevision),
                "Resulting server revision cannot be negative.");
        }

        OperationId = operationId;
        Outcome = outcome;
        ResultingServerRevision = resultingServerRevision;
        Message = message;
        ConflictingSnapshot = conflictingSnapshot;
    }

    public Guid OperationId { get; init; }

    public SyncOperationOutcome Outcome { get; init; }

    public long? ResultingServerRevision { get; init; }

    public string? Message { get; init; }

    public ProductSnapshot? ConflictingSnapshot { get; init; }

    public static SyncOperationResult Accept(Guid operationId, long resultingServerRevision) =>
        new(operationId, SyncOperationOutcome.Accepted, resultingServerRevision);

    public static SyncOperationResult Conflict(
        Guid operationId,
        string message,
        ProductSnapshot? conflictingSnapshot = null) =>
        new(operationId, SyncOperationOutcome.Conflict, null, message, conflictingSnapshot);

    public static SyncOperationResult Reject(Guid operationId, string message) =>
        new(operationId, SyncOperationOutcome.Rejected, null, message);
}
