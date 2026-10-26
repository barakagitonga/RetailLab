namespace RetailLab.Data;

/// <summary>
/// Audit-only metadata appended to an adjustStock receipt's recorded JSON.
/// Nullable fields reflect how far the server reached before making its decision.
/// </summary>
public sealed record AdjustmentReceiptMetadata(
    string DeltaPresence,
    int? Delta,
    string ReasonPresence,
    string? Reason,
    long? ResolvedBaseRevision,
    long? AppliedAgainstRevision,
    bool? Rebased);
