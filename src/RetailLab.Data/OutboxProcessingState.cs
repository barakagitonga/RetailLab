namespace RetailLab.Data;

/// <summary>
/// Lifecycle processing state for an operation in the local outbox.
/// </summary>
public enum OutboxProcessingState
{
    Pending = 1,
    Processed = 2,
    Conflicted = 3,
    Rejected = 4
}
