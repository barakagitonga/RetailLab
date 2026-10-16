namespace RetailLab.Core;

/// <summary>
/// Read-only facts used to decide whether a local database is safe to pair.
/// </summary>
public sealed record EligibilityReport
{
    public EligibilityReport(long productCount, long outboxEntryCount, long pendingOutboxCount)
    {
        if (productCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productCount));
        }

        if (outboxEntryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outboxEntryCount));
        }

        if (pendingOutboxCount < 0 || pendingOutboxCount > outboxEntryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pendingOutboxCount));
        }

        ProductCount = productCount;
        OutboxEntryCount = outboxEntryCount;
        PendingOutboxCount = pendingOutboxCount;
    }

    public long ProductCount { get; }

    public long OutboxEntryCount { get; }

    public long PendingOutboxCount { get; }

    public bool HasProducts => ProductCount > 0;

    public bool HasOutboxEntries => OutboxEntryCount > 0;

    public bool HasPendingOutboxWork => PendingOutboxCount > 0;
}
