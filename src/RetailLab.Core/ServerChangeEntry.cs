namespace RetailLab.Core;

/// <summary>
/// Authoritative ordered entry in the server change feed.
/// Sequenced monotonically for client cursor advancement.
/// </summary>
public sealed record ServerChangeEntry
{
    public ServerChangeEntry(
        long sequence,
        Guid productId,
        ProductChangeKind changeKind,
        long serverRevision,
        ProductSnapshot snapshot,
        DateTimeOffset occurredAtUtc)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence cannot be negative.");
        }

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

        ArgumentNullException.ThrowIfNull(snapshot);

        Sequence = sequence;
        ProductId = productId;
        ChangeKind = changeKind;
        ServerRevision = serverRevision;
        Snapshot = snapshot;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public long Sequence { get; init; }

    public Guid ProductId { get; init; }

    public ProductChangeKind ChangeKind { get; init; }

    public long ServerRevision { get; init; }

    public ProductSnapshot Snapshot { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }
}
