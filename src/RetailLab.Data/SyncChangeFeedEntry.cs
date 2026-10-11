using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Ordered, replayable entry in the authoritative server change feed.
/// Sequenced monotonically for client pull cursor synchronization.
/// </summary>
public sealed class SyncChangeFeedEntry
{
    private SyncChangeFeedEntry()
    {
    }

    public SyncChangeFeedEntry(
        Guid productId,
        ProductChangeKind changeKind,
        long serverRevision,
        string payload,
        DateTimeOffset occurredAtUtc)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        if (serverRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serverRevision),
                "Server revision cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(payload, nameof(payload));

        ProductId = productId;
        ChangeKind = changeKind;
        ServerRevision = serverRevision;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public long Sequence { get; private set; }

    public Guid ProductId { get; private set; }

    public ProductChangeKind ChangeKind { get; private set; }

    public long ServerRevision { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
