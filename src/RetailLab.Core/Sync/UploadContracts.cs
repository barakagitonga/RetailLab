namespace RetailLab.Core;

/// <summary>
/// Version numbers for synchronization wire contracts. Transports place these
/// values on the wire; Core itself remains independent of JSON and HTTP.
/// </summary>
public static class SyncProtocolVersions
{
    public const int Upload = 1;
}

/// <summary>
/// Transport-neutral request to upload ordered local product operations.
/// The client instance ID is used by a transport as the authenticated device
/// header and is not duplicated in the upload JSON body.
/// </summary>
public sealed record UploadRequest
{
    public UploadRequest(
        Guid clientInstanceId,
        Guid serverInstanceId,
        IReadOnlyList<ISyncProductOperation> operations)
    {
        if (clientInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Client instance ID cannot be empty.", nameof(clientInstanceId));
        }

        if (serverInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Server instance ID cannot be empty.", nameof(serverInstanceId));
        }

        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Any(operation => operation is null))
        {
            throw new ArgumentException("Upload operations cannot contain null entries.", nameof(operations));
        }

        ClientInstanceId = clientInstanceId;
        ServerInstanceId = serverInstanceId;
        Operations = operations;
    }

    public Guid ClientInstanceId { get; init; }

    public Guid ServerInstanceId { get; init; }

    public IReadOnlyList<ISyncProductOperation> Operations { get; init; }
}

/// <summary>
/// One terminal server decision for an uploaded operation.
/// </summary>
public sealed record UploadOperationResult
{
    public UploadOperationResult(
        Guid operationId,
        SyncOperationOutcome outcome,
        string? code,
        long? resultingServerRevision,
        ProductSnapshot? snapshot,
        bool replay)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        if (resultingServerRevision is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultingServerRevision),
                "Resulting server revision cannot be negative.");
        }

        if (outcome == SyncOperationOutcome.Accepted && !resultingServerRevision.HasValue)
        {
            throw new ArgumentException(
                "An accepted upload result requires a server revision.",
                nameof(resultingServerRevision));
        }

        if (outcome != SyncOperationOutcome.Accepted && string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "A conflict or rejection requires a stable result code.",
                nameof(code));
        }

        OperationId = operationId;
        Outcome = outcome;
        Code = code;
        ResultingServerRevision = resultingServerRevision;
        Snapshot = snapshot;
        Replay = replay;
    }

    public Guid OperationId { get; init; }

    public SyncOperationOutcome Outcome { get; init; }

    public string? Code { get; init; }

    public long? ResultingServerRevision { get; init; }

    public ProductSnapshot? Snapshot { get; init; }

    public bool Replay { get; init; }
}

/// <summary>
/// Transport-neutral response for a completely processed upload batch.
/// Non-success HTTP responses do not produce this contract.
/// </summary>
public sealed record UploadResponse
{
    public UploadResponse(
        int protocolVersion,
        Guid serverInstanceId,
        DateTimeOffset serverTimeUtc,
        IReadOnlyList<UploadOperationResult> results)
    {
        if (protocolVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(protocolVersion));
        }

        if (serverInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Server instance ID cannot be empty.", nameof(serverInstanceId));
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException("Server time is required.", nameof(serverTimeUtc));
        }

        ArgumentNullException.ThrowIfNull(results);
        if (results.Any(result => result is null))
        {
            throw new ArgumentException("Upload results cannot contain null entries.", nameof(results));
        }

        ProtocolVersion = protocolVersion;
        ServerInstanceId = serverInstanceId;
        ServerTimeUtc = serverTimeUtc.ToUniversalTime();
        Results = results;
    }

    public int ProtocolVersion { get; init; }

    public Guid ServerInstanceId { get; init; }

    public DateTimeOffset ServerTimeUtc { get; init; }

    public IReadOnlyList<UploadOperationResult> Results { get; init; }
}

/// <summary>
/// One pending outbox item returned in deterministic local sequence order.
/// </summary>
public sealed record PendingSyncOperation
{
    public PendingSyncOperation(long localSequence, ISyncProductOperation operation)
    {
        if (localSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localSequence));
        }

        ArgumentNullException.ThrowIfNull(operation);
        LocalSequence = localSequence;
        Operation = operation;
    }

    public long LocalSequence { get; init; }

    public ISyncProductOperation Operation { get; init; }
}
