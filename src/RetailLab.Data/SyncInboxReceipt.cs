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

    public SyncInboxReceipt(
        Guid operationId,
        SyncOperationOutcome outcome,
        string? recordedResult,
        DateTimeOffset processedAtUtc,
        Guid deviceId,
        string deviceDisplayName,
        string actorDisplayName,
        string deviceRole,
        string operationHash,
        long? resultServerRevision,
        string? resultSnapshot)
        : this(operationId, outcome, recordedResult, processedAtUtc)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(deviceDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceRole);
        ValidateOperationHash(operationHash);
        if (resultServerRevision is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultServerRevision));
        }

        DeviceId = deviceId;
        DeviceDisplayName = deviceDisplayName.Trim();
        ActorDisplayName = actorDisplayName.Trim();
        DeviceRole = deviceRole.Trim();
        OperationHash = operationHash;
        ResultServerRevision = resultServerRevision;
        ResultSnapshot = resultSnapshot;
    }

    public Guid OperationId { get; private set; }

    public SyncOperationOutcome Outcome { get; private set; }

    public string? RecordedResult { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public Guid? DeviceId { get; private set; }

    public string? DeviceDisplayName { get; private set; }

    public string? ActorDisplayName { get; private set; }

    public string? DeviceRole { get; private set; }

    public string? OperationHash { get; private set; }

    public long? ResultServerRevision { get; private set; }

    public string? ResultSnapshot { get; private set; }

    private static void ValidateOperationHash(string operationHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationHash);
        if (operationHash.Length != 64 ||
            operationHash.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            throw new ArgumentException(
                "Operation hash must be 64 lowercase hexadecimal characters.",
                nameof(operationHash));
        }
    }
}
