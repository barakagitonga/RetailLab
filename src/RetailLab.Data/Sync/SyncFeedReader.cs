using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public sealed class SyncFeedReader(RetailLabDbContext dbContext) : ISyncFeedReader
{
    public async Task<long> GetHeadSequenceAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SyncChangeFeedEntries
            .AsNoTracking()
            .Select(entry => (long?)entry.Sequence)
            .MaxAsync(cancellationToken) ?? 0L;
    }

    public async Task<PullResponse> ReadAfterAsync(
        long afterSequence,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterSequence));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var headSequence = await GetHeadSequenceAsync(cancellationToken);
        var persistedEntries = await dbContext.SyncChangeFeedEntries
            .AsNoTracking()
            .Where(entry => entry.Sequence > afterSequence)
            .OrderBy(entry => entry.Sequence)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = persistedEntries.Count > limit;
        var entries = persistedEntries
            .Take(limit)
            .Select(ToContract)
            .ToList();

        return new PullResponse(entries, headSequence, hasMore);
    }

    private static ServerChangeEntry ToContract(SyncChangeFeedEntry entry)
    {
        var snapshot = JsonSerializer.Deserialize<ProductSnapshot>(entry.Payload)
            ?? throw new InvalidOperationException(
                $"Change-feed entry {entry.Sequence} has an invalid product snapshot.");

        return new ServerChangeEntry(
            entry.Sequence,
            entry.ProductId,
            entry.ChangeKind,
            entry.ServerRevision,
            snapshot,
            entry.OccurredAtUtc);
    }
}
